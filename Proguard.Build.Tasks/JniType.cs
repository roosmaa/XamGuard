using System;
using System.Collections.Generic;

namespace BitterFudge.Proguard.Build
{
    class JniType
    {
        readonly string name;
        readonly HashSet<JniMember> members = new HashSet<JniMember> ();

        public JniType (string name)
        {
            this.name = name;
        }

        public string Name {
            get { return name; }
        }

        public ISet<JniMember> Members {
            get { return members; }
        }

        public override string ToString ()
        {
            return Name.Replace ('/', '.');
        }
    }
}
    