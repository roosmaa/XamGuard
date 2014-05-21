using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System.Collections.Generic;

namespace BitterFudge.Proguard.Build.Tasks
{
    public class Proguard : ToolTask
    {
        [Required]
        public string JavaPlatformJarPath { get; set; }

        [Required]
        public string MonoPlatformJarPath { get; set; }

        [Required]
        public string CompiledJavaDirectory { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        [Required]
        public string AndroidIntermediateAssetsDir { get; set; }

        [Required]
        public string MainAssembly { get; set; }

        public ITaskItem[] LibraryProjects { get; set; }

        public ITaskItem[] AdditionalLibraries { get; set; }

        public string UserConfig { get; set; }

        protected override string GenerateFullPathToTool ()
        {
            return Path.Combine (ToolPath, ToolExe);
        }

        protected override string ToolName {
            get {
                return OS.IsWindows ? "proguard.bat" : "proguard.sh";
            }
        }

        private string Config { get; set; }

        public override bool Execute ()
        {
            string proGuardHome = Environment.GetEnvironmentVariable("PROGUARD_HOME");
            if (string.IsNullOrEmpty(proGuardHome))
            {
                proGuardHome = Directory.GetParent(ToolPath).Name;
                this.EnvironmentVariables = new string[] {string.Format(@"PROGUARD_HOME={0}", proGuardHome)};
            }

            Config = Path.GetTempFileName ();
            try {
                GenerateConfiguration ();

                return base.Execute ();
            } finally {
                File.Delete (Config);
            }
        }

        protected override string GenerateCommandLineCommands ()
        {
            var builder = new CommandLineBuilder ();

            var monoPlatformJarPath = MonoPlatformJarPath;
            if (OS.IsWindows)
            {
                // Escape paths that have spaces or parentheticals.
                monoPlatformJarPath = "\"" + MonoPlatformJarPath + "\"";
            }

            builder.AppendSwitchIfNotNull ("-injars ", CompiledJavaDirectory);
            builder.AppendSwitchIfNotNull ("-injars ", LibraryProjects, ":");
            builder.AppendSwitchIfNotNull ("-injars ", AdditionalLibraries, ":");
            builder.AppendSwitchIfNotNull ("-libraryjars ", JavaPlatformJarPath);
            builder.AppendSwitchIfNotNull ("-libraryjars ", monoPlatformJarPath);
            builder.AppendSwitchIfNotNull ("-outjar ", OutputDirectory);
            builder.AppendSwitchIfNotNull ("-include ", Config);
            if (File.Exists (UserConfig)) {
                builder.AppendSwitchIfNotNull ("-include ", UserConfig);
            }
            return builder.ToString ();
        }

        private void GenerateConfiguration ()
        {
            var resolver = new DefaultAssemblyResolver ();
            resolver.AddSearchDirectory (AndroidIntermediateAssetsDir);

            var assembly = AssemblyDefinition.ReadAssembly (MainAssembly, new ReaderParameters () {
                AssemblyResolver = resolver,
            });

            // Assumption is that the assemblies have been stripped already, thus all of the referencing assemblies
            // only have recerences to existing Java classes
            var extTypes = assembly.Modules
                .SelectMany (m => m.AssemblyReferences
                    .Where (r => r.Name != "Mono.Android")
                    .Select (r => m.AssemblyResolver.Resolve (r)))
                .SelectMany (a => a.Modules)
                .SelectMany (m => m.Types)
                .SelectMany (t => new List<TypeDefinition> () { t }.Concat (t.NestedTypes));

            var indexer = new JniIndexer ();
            var types = indexer.Crawl (extTypes);

            // Generate Proguard configuration
            using (var sw = new StreamWriter (Config)) {
                // Keep all generated resource files:
                sw.WriteLine ("-keepclassmembers class **.R$* {");
                sw.WriteLine ("\tpublic static <fields>;");
                sw.WriteLine ("}");

                // Keep all mono java & ACWs:
                sw.WriteLine ("-keep class mono.** { *; }");
                sw.WriteLine ("-keep class * implements mono.android.IGCUserPeer { *; }");

                // Keep Java used by bindings:
                foreach (var t in types) {
                    sw.Write ("-keep class {0}", t);

                    if (t.Members.Count > 0) {
                        sw.WriteLine (" {");

                        if (t.Members.Any (m => m.Signature == null)) {
                            sw.WriteLine ("\t<fields>;");
                        }

                        foreach (var m in t.Members.Where(m => m.Signature != null)) {
                            sw.Write ('\t');
                            sw.Write (m);
                            sw.WriteLine (';');
                        }

                        sw.Write ("}");
                    }
                    sw.WriteLine ();
                }
            }
        }
    }
}
