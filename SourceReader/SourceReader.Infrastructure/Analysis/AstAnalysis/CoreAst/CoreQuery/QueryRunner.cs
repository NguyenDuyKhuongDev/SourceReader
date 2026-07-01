using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Text;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery
{
    public static class QueryRunner
    {
        /// <summary>
        /// This dicitonary is used to store a query pattern for each language , which will be used to extract symbol from ast , like class, method, interface, struct, enum, record, constructor, property and namespace , ast is all of the node in source but thought this pattern we just get what we want like class , method ..vv.
        /// </summary>
        private static readonly Dictionary<string, string> Patterns =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["C#"] = """
            (class_declaration            name: (identifier) @class)
            (interface_declaration        name: (identifier) @interface)
            (struct_declaration           name: (identifier) @struct)
            (enum_declaration             name: (identifier) @enum)
            (record_declaration           name: (identifier) @record)
            (method_declaration           name: (identifier) @method)
            (constructor_declaration      name: (identifier) @constructor)
            (property_declaration         name: (identifier) @property)
            (namespace_declaration        name: (qualified_name) @namespace)
            (file_scoped_namespace_declaration name: (qualified_name) @namespace)
            """,
                ["Python"] = """
            (class_definition    name: (identifier) @class)
            (function_definition name: (identifier) @method)
            """,
                ["TypeScript"] = """
            (class_declaration      name: (type_identifier)     @class)
            (interface_declaration  name: (type_identifier)     @interface)
            (function_declaration   name: (identifier)          @method)
            (method_definition      name: (property_identifier) @method)
            """,
                ["JavaScript"] = """
            (class_declaration    name: (identifier)          @class)
            (function_declaration name: (identifier)          @method)
            (method_definition    name: (property_identifier) @method)
            """,
                ["Go"] = """
            (function_declaration name: (identifier)       @method)
            (method_declaration   name: (field_identifier) @method)
            (type_spec            name: (type_identifier)  @struct)
            """,
                ["Java"] = """
            (class_declaration     name: (identifier) @class)
            (interface_declaration name: (identifier) @interface)
            (method_declaration    name: (identifier) @method)
            (enum_declaration      name: (identifier) @enum)
            """,
                ["Rust"] = """
            (struct_item  name: (type_identifier) @struct)
            (enum_item    name: (type_identifier) @enum)
            (fn_item      name: (identifier)      @method)
            (trait_item   name: (type_identifier) @interface)
            """,
            };

        public static string? GetPattern(string lang) =>
            Patterns.GetValueOrDefault(lang);

        /// <summary>
        /// run querry in AST to extract symbol base on query pattern 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="root"></param>
        /// <param name="fileId"></param>
        /// <param name="lang"></param>
        /// <returns></returns>
        public static List<SymbolRecord> Run(
            Query query,
            Node root,
            int fileId,
            string lang)
        {
            var symbols = new List<SymbolRecord>();
            var nextId = 1;

            // TreeSitter.DotNet: query.Execute(root).Captures
            foreach (var capture in query.Execute(root).Captures)
            {
                var kind = CaptureNameToKind(capture.Name);
                if (kind is null) continue;

                // capture.Node.Text — lấy text trực tiếp, không cần source string
                var name = capture.Node.Text;
                if (string.IsNullOrWhiteSpace(name)) continue;

                symbols.Add(new SymbolRecord(
                    symbolId: nextId++,
                    fileId: fileId,
                    name: name,
                    kind: kind.Value,
                    startLine: (int)capture.Node.StartPosition.Row+ 1,
                    endLine: (int)capture.Node.EndPosition.Row + 1,
                    parentName: ResolveParent(capture.Node, lang)
                ));
            }

            return symbols;
        }

        // Tìm parent name bằng cách leo lên ancestor tree
        // Query không tự biết parent context nên cần helper này
        private static string? ResolveParent(Node node, string lang)
        {
            var current = node.Parent;
            while (current is not null)
            {
                var kind = NodeKindMap.Resolve(current.Type, lang);
                if (kind is SymbolKind.Class or SymbolKind.Namespace
                         or SymbolKind.Interface or SymbolKind.Struct
                         or SymbolKind.Record)
                {
                    return current.GetChildrenForField("name").FirstOrDefault()?.Text;
                }
                current = current.Parent;
            }
            return null;
        }

        private static SymbolKind? CaptureNameToKind(string name) => name switch
        {
            "class" => SymbolKind.Class,
            "interface" => SymbolKind.Interface,
            "struct" => SymbolKind.Struct,
            "enum" => SymbolKind.Enum,
            "record" => SymbolKind.Record,
            "method" => SymbolKind.Method,
            "constructor" => SymbolKind.Constructor,
            "property" => SymbolKind.Property,
            "namespace" => SymbolKind.Namespace,
            _ => null
        };
    }
}
