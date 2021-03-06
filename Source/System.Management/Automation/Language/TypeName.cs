﻿// Copyright (C) Pash Contributors. License: GPL/BSD. See https://github.com/Pash-Project/Pash/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace System.Management.Automation.Language
{
    public class TypeName : ITypeName
    {
        private static readonly Dictionary<string, Type> TypeAccelerators = 
            new Dictionary<string, Type>(StringComparer.CurrentCultureIgnoreCase)
        {
            { "int", typeof(int) },
            { "long", typeof(long) },
            { "string", typeof(string) },
            { "char", typeof(char) },
            { "bool", typeof(bool) },
            { "byte", typeof(byte) },
            { "double", typeof(double) },
            { "decimal", typeof(decimal) },
            { "float", typeof(float) },
            { "single", typeof(float) },
            { "regex", typeof(Text.RegularExpressions.Regex) },
            { "array", typeof(Array) },
            { "xml", typeof(Xml.XmlDocument) },
            { "scriptblock", typeof(ScriptBlock) },
            { "switch", typeof(SwitchParameter)  },
            { "hashtable", typeof(Collections.Hashtable) },
            { "type", typeof(Type) },
            { "ipaddress", typeof(Net.IPAddress) },
            { "psobject", typeof(System.Management.Automation.PSObject) }
            // TODO: Next accelerators seems to be PowerShell and Windows-specific. Sort them out.
            //{ "ref", typeof(System.Management.Automation.PSReference) },
            //pscustomobject	System.Management.Automation.PSObject
            //psmoduleinfo	System.Management.Automation.PSModuleInfo
            //powershell	System.Management.Automation.PowerShell
            //runspacefactory	System.Management.Automation.Runspaces.RunspaceFactory
            //runspace	System.Management.Automation.Runspaces.Runspace
            //wmi	System.Management.ManagementObject
            //wmisearcher	System.Management.ManagementObjectSearcher
            //wmiclass	System.Management.ManagementClass
            //adsi	System.DirectoryServices.DirectoryEntry
            //adsisearcher	System.DirectoryServices.DirectorySearcher
            //accelerators	System.Management.Automation.TypeAccelerators
        };
        private static readonly Regex _arrayRegex = new Regex(@"^\[,*\]$");

        private int _dimensions = 0;
        readonly Type Type;

        public TypeName(Type type)
        {
            this.Type = type;
        }

        public TypeName(string name)
        {
            this.Name = name;
            if (Name.EndsWith("]")) // parse array or generic type
            {
                var beginBracket = Name.IndexOf('[');
                if (beginBracket > 0)
                {
                    var brackets = Name.Substring(beginBracket, Name.Length - beginBracket);
                    if (_arrayRegex.IsMatch(brackets))
                    {
                        Name = Name.Substring(0, beginBracket);
                        _dimensions = brackets.Length - 1;
                    }
                }
                // TODO: support generic args
                // otherwise we do nothing for now
            }
        }

        internal TypeName(string name, int dimensions)
        {
            Name = name;
            _dimensions = dimensions;
        }

        public string AssemblyName
        {
            get;
            private set;
        }

        public IScriptExtent Extent
        {
            get;
            private set;
        }

        public string FullName
        {
            get;
            private set;
        }

        public bool IsArray
        {
            get
            {
                return _dimensions > 0;
            }
        }

        public bool IsGeneric
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }

        public Type GetReflectionAttributeType()
        {
            throw new NotImplementedException();
        }

        public Type GetReflectionType()
        {
            var rawType = GetRawReflectionType();
            if (_dimensions < 1)
            {
                return rawType;
            }
            return rawType.MakeArrayType(_dimensions);
        }

        private Type GetRawReflectionType()
        {
            // We act correspondingly to the notes in §3.9 of PowerShell Language Specification.
            Type type;
            if (TypeAccelerators.TryGetValue(Name, out type))
            {
                return type;
            }

            /*
             * In PowerShell, I ran:
             *      [System.AppDomain]::CurrentDomain.GetAssemblies() | select -ExpandProperty fullname | sort | clip
             *  And removed a couple items.
             */
            var defaultSearchAssemblies = new[] {
                // "Anonymously Hosted DynamicMethods Assembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
                "Microsoft.CSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                // "Microsoft.Management.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                // "Microsoft.PowerShell.Commands.Management, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                // "Microsoft.PowerShell.Commands.Utility, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                // "Microsoft.PowerShell.ConsoleHost, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                // "Microsoft.PowerShell.Security, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                // "PSEventHandler, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                // "System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                // "System.Configuration.Install, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                // "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                // "System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                // "System.Data.SqlXml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                // "System.DirectoryServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                // "System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                // "System.Management.Automation, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                // "System.Numerics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                // "System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                // "System.Transactions, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            };

            // I tried to write this in LINQ but it didn't come out clean.
            foreach (var item in defaultSearchAssemblies)
            {
                var assembly = Assembly.Load(item);
                type = type ?? assembly.GetType(this.Name, false, true);
                type = type ?? assembly.GetType("System." + this.Name, false, true);
            }

            if (type != null) return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(this.Name, false, true);
                if (type != null)
                    return type;
            }

            // TODO: Parse generic types.

            throw new ArgumentException(String.Format("Unknown type '{0}'. The type cannot be resolved", Name));
        }

        public override string ToString()
        {
            return string.Format("[{0}]", this.Name);
        }
    }
}
