using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CSharp;

namespace GenerateEnums
{
    public class GenerateEnum
    {
        #region Members
        // the name of the enum
        private string _enumName;
        // the list of entries within the enum
        private List<EnumEntry> _entries = new List<EnumEntry>();

        private int _enumValueDefault = -1;

        private bool _autoResovleConflicts;

        private string _namespace;
        #endregion

        #region Constructor
        /// <summary>
        /// 
        /// </summary>
        /// <param name="enumTypeName">The name of the enum you wish to create</param>
        /// <param name="autoResolveConflicts"></param>
        public GenerateEnum(string enumTypeName, string myNamespace, bool autoResolveConflicts = false)
        {
            _enumName = RemoveInvalidClassCharacters(enumTypeName);
            _autoResovleConflicts = autoResolveConflicts;
            _namespace = myNamespace;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Add an entry to the enum called enumName
        /// </summary>
        /// <param name="enumName"></param>
        public void AddEnumEntry(string enumName)
        {
            AddEnumEntry(enumName, _enumValueDefault);
        }

        /// <summary>
        /// Add an entry to the enum called enumName of value enumValue
        /// </summary>
        /// <param name="enumName"></param>
        /// <param name="enumValue"></param>
        public void AddEnumEntry(string enumName, int enumValue)
        {
            AddEnumEntry(enumName, enumValue, (EnumAttribute)null);
        }

        /// <summary>
        /// Add an entry to the enum called enumName of value enumValue with an attribute on it
        /// </summary>
        /// <param name="enumName"></param>
        /// <param name="enumValue"></param>
        /// <param name="attribute"></param>
        public void AddEnumEntry(string enumName, int enumValue, EnumAttribute attribute)
        {
            if (attribute != null)
                AddEnumEntry(enumName, enumValue, new List<EnumAttribute>() { attribute });
            else
                AddEnumEntry(enumName, enumValue, (List<EnumAttribute>)null);
        }

        /// <summary>
        /// Add an entry to the enum called enumName of value enumValue with several attributes on it
        /// </summary>
        /// <param name="enumName"></param>
        /// <param name="enumValue"></param>
        /// <param name="attributes"></param>
        public void AddEnumEntry(string enumName, int enumValue, List<EnumAttribute> attributes)
        {
            var potentialEnumEntry = new EnumEntry() { EnumName = enumName, EnumValue = enumValue, Attributes = attributes};

            if (_entries.Any<EnumEntry>(new Func<EnumEntry, bool>(e =>
                { return (e.EnumName == potentialEnumEntry.EnumName) || ((e.EnumValue == potentialEnumEntry.EnumValue) && (e.EnumValue != _enumValueDefault)); })))
            {
                if (!_autoResovleConflicts)
                    throw new ArgumentException(string.Format("Enum name {0} or value {1} already defined", enumName, enumValue));
                else
                {
                    Regex r = new Regex("_[0-9]+$");
                    var result = r.Match(enumName);

                    if (!result.Success)
                    {
                        enumName += "_1";
                    }
                    else
                    {
                        r = new Regex("[0-9]+$");
                        int num = Int32.Parse(r.Match(enumName).Value);
                        enumName = r.Replace(enumName, (++num).ToString());
                    }
                    AddEnumEntry(enumName, enumValue, attributes);
                }
            }
            else
            {
                _entries.Add(potentialEnumEntry);
            }
        }        

        /// <summary>
        /// Generates a class file for each enum in the output 
        /// </summary>
        public void WriteToFile(string outputPath)
        {
            CodeDomProvider provider = new CSharpCodeProvider();
            var codenamespace = new CodeNamespace(_namespace);
            var compileUnit = new CodeCompileUnit();
           
            var codeClass = new CodeTypeDeclaration(_enumName) { IsEnum = true };

            foreach (var entry in _entries)
            {
                // create a new enum entry
                var codeMember = new CodeMemberField { Name = entry.EnumName };
                // if there is an integer value specificed, set it on the enum
                if (entry.EnumValue != _enumValueDefault)
                    codeMember.InitExpression = new CodePrimitiveExpression(entry.EnumValue);

                // if the entry has attributes add them
                if (entry.Attributes != null)
                {
                    foreach (var attribute in entry.Attributes)
                    {
                        // get the attribute name
                        string attributeName = attribute.Item1.Name;
                        // create the attribute argument
                        var caa = new CodeAttributeArgument[] { new CodeAttributeArgument(attribute.Item2) };
                        // create the attribute declaration
                        var cad = new CodeAttributeDeclaration(attributeName, caa);

                        // add the attribute declaration to the enum entry
                        codeMember.CustomAttributes.Add(cad);
                        
                        // add the namespace of the attribute to the usings at the beginning
                        codenamespace.Imports.Add(new CodeNamespaceImport(attribute.Item1.Namespace));
                    }
                }

                // add the entry to the class
                codeClass.Members.Add(codeMember);
            }

            codenamespace.Types.Add(codeClass);
            compileUnit.Namespaces.Add(codenamespace);

            using (TextWriter textWriter = new StreamWriter(string.Format(@"{0}\{1}.cs", outputPath, _enumName)))
            {
                var options = new CodeGeneratorOptions { BracingStyle = "C", BlankLinesBetweenMembers = false };
                provider.GenerateCodeFromNamespace(codenamespace, textWriter, options);
            }
            codenamespace.Types.Clear();            
        }
        #endregion

        #region Private Helper Methods
        /// <summary>
        /// Removes invalid characters from a class name
        /// </summary>
        /// <param name="className">Name of a class</param>
        /// <returns></returns>
        private static string RemoveInvalidClassCharacters(string className)
        {
            // Remove invalid characters from everywhere and replace them with_
            var level1Correction = Regex.Replace(className, "[^A-Za-z0-9_]+", "_");

            // Make sure the name does not begin with a number or a _. if it does remove them until we hit a character
            return level1Correction.TrimStart(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '_' });
        }

        /// <summary>
        /// Removes the invalid characters from an enum name.
        /// </summary>
        /// <param name="enumValueName">Name of the enum value.</param>
        /// <returns></returns>
        internal static string RemoveInvalidEnumCharacters(string enumValueName)
        {
            // Remove invalid characters from everywhere and replace them with_
            var level1Correction = Regex.Replace(enumValueName, "[^A-Za-z0-9]+", "_");

            // Make sure the name does not begin with a number. if it does add an _ to the beginning
            var number = 0;
            var beginsWithNumber = int.TryParse(level1Correction[0].ToString(), out number);

            return beginsWithNumber ? level1Correction.Insert(0, "_") : level1Correction;
        }
        #endregion

        #region Container classes
        /// <summary>
        /// EnumEntry class is to contain all of the information for a single entry within an enum
        /// </summary>
        private class EnumEntry
        {
            private string enumName;
            public string EnumName 
            {
                get { return enumName; }
                set
                {
                    enumName = RemoveInvalidEnumCharacters(value);
                }
            }
            public int EnumValue { get; set; }
            public List<EnumAttribute> Attributes { get; set; }
        }

        /// <summary>
        /// Contains all the information to generate an attribute on an EnumEntry
        /// </summary>
        public class EnumAttribute : Tuple<Type, CodeExpression>
        {
            public EnumAttribute(Type attributeType, CodeExpression attributeArgument)
                : base(attributeType, attributeArgument)
            { }
        }
        #endregion
    }
}