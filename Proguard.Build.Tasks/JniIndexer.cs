using System;
using System.Linq;
using Mono.Cecil;
using System.Collections.Generic;

namespace BitterFudge.Proguard.Build
{
    class JniIndexer
    {
        readonly Dictionary<string, JniType> typeMap = new Dictionary<string, JniType> ();

        public List<JniType> Crawl (IEnumerable<TypeDefinition> types)
        {
            try {
                foreach (var t in types) {
                    var jniType = GetJniType (t);
                    if (jniType == null)
                        continue;

                    // Get all class registration attributes
                    var regAttrs = t.Events.Cast<IMemberDefinition> ()
                        .Union (t.Fields.Cast<IMemberDefinition> ())
                        .Union (t.Properties.Cast<IMemberDefinition> ())
                        .Union (t.Methods.Cast<IMemberDefinition> ())
                        .SelectMany (m => m.CustomAttributes)
                        .Where (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");

                    foreach (var attr in regAttrs) {
                        String jniName = null;
                        String jniSig = null;
                        if (attr.ConstructorArguments.Count > 0) {
                            jniName = (string)attr.ConstructorArguments [0].Value;
                        }
                        if (attr.ConstructorArguments.Count > 1) {
                            jniSig = (string)attr.ConstructorArguments [1].Value;
                        }
                        if (jniName == null)
                            continue;

                        jniType.Members.Add (new JniMember (jniName, jniSig));
                    }
                }

                return typeMap.Values.ToList ();
            } finally {
                typeMap.Clear ();
            }
        }

        JniType GetJniType (TypeDefinition typeDef)
        {
            var attr = typeDef.CustomAttributes
                .SingleOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");

            var name = attr != null ? (string)attr.ConstructorArguments [0].Value : null;
            if (name == null)
                return null;

            JniType jniType;
            if (!typeMap.TryGetValue (name, out jniType)) {
                jniType = new JniType (name);
                typeMap [name] = jniType;
            }
            return jniType;
        }
    }
}
