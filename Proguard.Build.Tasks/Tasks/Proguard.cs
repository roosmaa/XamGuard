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

        public string LibraryProjectsDirectory { get; set; }

        public ITaskItem[] AdditionalLibraries { get; set; }

        public string Config { get; set; }

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

        private string JavaOutputDirectory { get; set; }

        private string AdditionalLibrariesOutputDirectory { get; set; }

        private string LibraryProjectsOutputDirectory { get; set; }

        private string LibraryProjectsTempDirectory { get; set; }

        public override bool Execute ()
        {
            string proGuardHome = Environment.GetEnvironmentVariable("PROGUARD_HOME");
            if (string.IsNullOrEmpty(proGuardHome))
            {
                proGuardHome = Directory.GetParent(ToolPath).Name;
                this.EnvironmentVariables = new string[] {string.Format(@"PROGUARD_HOME={0}", proGuardHome)};
            }

            // Prepare paths
            JavaOutputDirectory = Path.Combine (OutputDirectory, "java");
            AdditionalLibrariesOutputDirectory = Path.Combine (OutputDirectory, "additional");
            LibraryProjectsOutputDirectory = Path.Combine (OutputDirectory, "library_projects");
            var tempDirectory = Path.Combine (OutputDirectory, "tmp");
            LibraryProjectsTempDirectory = Path.Combine (tempDirectory, "library_projects");

            MoveFiles (LibraryProjectsDirectory, "*.jar", SearchOption.TopDirectoryOnly, LibraryProjectsTempDirectory, "library project jar");
            GenerateConfiguration ();
            var success = base.Execute ();

            if (success) {
                CopyFiles (LibraryProjectsOutputDirectory, "*.jar", SearchOption.AllDirectories, LibraryProjectsDirectory, "processed library project jar");
            }

            return success;
        }

        private void MoveFiles (string sourceDirectory, string searchPattern, SearchOption searchOption, string destDirectory, string fileDescription)
        {
            if (Directory.Exists (destDirectory))
                Directory.Delete (destDirectory, true);

            var files = Directory.GetFiles (sourceDirectory, searchPattern, searchOption);
            if (files.Length < 1)
                return;

            foreach (var file in files) {
                // Trim project directory from the name
                var fileSubpath = file;
                if (fileSubpath.StartsWith (sourceDirectory)) {
                    fileSubpath = fileSubpath.Substring (sourceDirectory.Length + 1 /* path separator */);
                }

                var sourcePath = Path.Combine (sourceDirectory, fileSubpath);
                var destPath = Path.Combine (destDirectory, fileSubpath);
                var destDir = Path.GetDirectoryName (destPath);

                Log.LogMessage ("Moving {0} {1} to {2}", fileDescription, fileSubpath, destDir);
                Directory.CreateDirectory (destDir);
                File.Move (sourcePath, destPath);
            }
        }

        private void CopyFiles (string sourceDirectory, string searchPattern, SearchOption searchOption, string destDirectory, string fileDescription)
        {
            var files = Directory.GetFiles (sourceDirectory, searchPattern, searchOption);
            if (files.Length < 1)
                return;

            foreach (var file in files) {
                // Trim project directory from the name
                var fileSubpath = file;
                if (fileSubpath.StartsWith (sourceDirectory)) {
                    fileSubpath = fileSubpath.Substring (sourceDirectory.Length + 1 /* path separator */);
                }

                var sourcePath = Path.Combine (sourceDirectory, fileSubpath);
                var destPath = Path.Combine (destDirectory, fileSubpath);
                var destDir = Path.GetDirectoryName (destPath);

                Log.LogMessage ("Copying {0} {1} to {2}", fileDescription, fileSubpath, destDir);
                Directory.CreateDirectory (destDir);
                File.Copy (sourcePath, destPath);
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
            builder.AppendSwitchIfNotNull ("-outjar ", JavaOutputDirectory);
            if (AdditionalLibraries != null && AdditionalLibraries.Length > 0) {
                builder.AppendSwitchIfNotNull ("-injars ", AdditionalLibraries, ":");
                builder.AppendSwitchIfNotNull ("-outjar ", AdditionalLibrariesOutputDirectory);
            }
            if (Directory.Exists (LibraryProjectsTempDirectory)) {
                builder.AppendSwitchIfNotNull ("-injars ", LibraryProjectsTempDirectory);
                builder.AppendSwitchIfNotNull ("-outjar ", LibraryProjectsOutputDirectory);
            }
            builder.AppendSwitchIfNotNull ("-libraryjars ", JavaPlatformJarPath);
            builder.AppendSwitchIfNotNull ("-libraryjars ", monoPlatformJarPath);
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
