using System;
using System.Text;
using System.Collections.Generic;

namespace BitterFudge.Proguard.Build
{
    class JniMember
    {
        readonly string name;
        readonly string signature;

        public JniMember (string name, string signature)
        {
            this.name = name;
            this.signature = signature;
        }

        public string Name {
            get { return name; }
        }

        public string Signature {
            get { return signature; }
        }

        public override string ToString ()
        {
            var name = Name;
            if (name == ".ctor")
                name = "<init>";

            var e = Signature.GetEnumerator ();
            if (!e.MoveNext ())
                throw new FormatException ("Invalid signature");

            var str = Parse (e, name);

            if (e.MoveNext ())
                throw new FormatException ("Invalid signature");

            return str;
        }

        string Parse (CharEnumerator e, string name = null)
        {
            return ParseMethod (e, name) ?? ParsePrimitive (e, name) ?? ParseArray (e, name) ?? ParseClass (e, name);
        }

        string ParseMethod (CharEnumerator e, string name = null)
        {
            if (e.Current != '(')
                return null;

            List<string> args = new List<string> ();
            if (!e.MoveNext ())
                throw new FormatException ();
            while (e.Current != ')') {
                args.Add (Parse (e));
                if (!e.MoveNext ())
                    throw new FormatException ();
            }

            if (!e.MoveNext ())
                throw new FormatException ();
            var returnType = Parse (e);

            var sb = new StringBuilder ();
            if (returnType != "void" || name != "<init>") {
                sb.Append (returnType);
                sb.Append (' ');
            }
            if (name != null) {
                sb.Append (name);
            }
            sb.Append ("(");
            sb.Append (String.Join (", ", args));
            sb.Append (")");
            return sb.ToString ();
        }

        string ParsePrimitive (CharEnumerator e, string name = null)
        {
            string type;
            switch (e.Current) {
            case 'V':
                type = "void";
                break;
            case 'Z':
                type = "boolean";
                break;
            case 'B':
                type = "byte";
                break;
            case 'C':
                type = "char";
                break;
            case 'S':
                type = "short";
                break;
            case 'I':
                type = "int";
                break;
            case 'J':
                type = "long";
                break;
            case 'F':
                type = "float";
                break;
            case 'D':
                type = "double";
                break;
            default:
                return null;
            }

            if (name == null)
                return type;
            return String.Concat (type, " ", name);
        }

        string ParseArray (CharEnumerator e, string name = null)
        {
            if (e.Current != '[')
                return null;

            if (!e.MoveNext ())
                throw new FormatException ();

            var type = Parse (e);
            if (name == null)
                return String.Concat (type, "[]");
            return String.Concat (type, "[] ", name);
        }

        string ParseClass (CharEnumerator e, string name = null)
        {
            if (e.Current != 'L')
                return null;

            var sb = new StringBuilder ();
            if (!e.MoveNext ())
                throw new FormatException ();
            while (e.Current != ';') {
                sb.Append (e.Current == '/' ? '.' : e.Current);
                if (!e.MoveNext ())
                    throw new FormatException ();
            }

            if (name != null) {
                sb.Append (' ');
                sb.Append (name);
            }
            return sb.ToString ();
        }

        public override bool Equals (object obj)
        {
            if (obj == null)
                return false;

            var rec = obj as JniMember;
            if (rec == null)
                return false;

            return this.Name == rec.Name && this.Signature == rec.Signature;
        }

        public override int GetHashCode ()
        {
            var hash = name != null ? name.GetHashCode () : 0;
            hash ^= signature != null ? signature.GetHashCode () : 0;
            return hash;
        }

        public static bool operator == (JniMember a, JniMember b)
        {
            return ReferenceEquals (a, b) || (!ReferenceEquals (a, null) && a.Equals (b));
        }

        public static bool operator != (JniMember a, JniMember b)
        {
            return !(a == b);
        }
    }
}
