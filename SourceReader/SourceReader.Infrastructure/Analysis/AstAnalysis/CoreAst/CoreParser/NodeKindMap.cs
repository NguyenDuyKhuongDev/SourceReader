using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public static class NodeKindMap
    {
        // node.Type (string từ TreeSitter grammar) → SymbolKind
        public static SymbolKind? Resolve(string nodeType, string lang) =>
            lang switch
            {
                "C#" => nodeType switch
                {
                    "class_declaration" => SymbolKind.Class,
                    "interface_declaration" => SymbolKind.Interface,
                    "struct_declaration" => SymbolKind.Struct,
                    "enum_declaration" => SymbolKind.Enum,
                    "record_declaration" => SymbolKind.Record,
                    "method_declaration" => SymbolKind.Method,
                    "constructor_declaration" => SymbolKind.Constructor,
                    "property_declaration" => SymbolKind.Property,
                    "field_declaration" => SymbolKind.Field,
                    "namespace_declaration" => SymbolKind.Namespace,
                    "file_scoped_namespace_declaration" => SymbolKind.Namespace,
                    _ => null
                },
                "Python" => nodeType switch
                {
                    "class_definition" => SymbolKind.Class,
                    "function_definition" => SymbolKind.Method,
                    _ => null
                },
                "TypeScript" or "JavaScript" => nodeType switch
                {
                    "class_declaration" => SymbolKind.Class,
                    "method_definition" => SymbolKind.Method,
                    "function_declaration" => SymbolKind.Method,
                    "interface_declaration" => SymbolKind.Interface,
                    _ => null
                },
                "Go" => nodeType switch
                {
                    "function_declaration" => SymbolKind.Method,
                    "method_declaration" => SymbolKind.Method,
                    "type_spec" => SymbolKind.Struct,
                    _ => null
                },
                "Java" => nodeType switch
                {
                    "class_declaration" => SymbolKind.Class,
                    "interface_declaration" => SymbolKind.Interface,
                    "method_declaration" => SymbolKind.Method,
                    "enum_declaration" => SymbolKind.Enum,
                    _ => null
                },
                "Rust" => nodeType switch
                {
                    "struct_item" => SymbolKind.Struct,
                    "enum_item" => SymbolKind.Enum,
                    "fn_item" => SymbolKind.Method,
                    "impl_item" => SymbolKind.Class,   // impl block ~ class
                    "trait_item" => SymbolKind.Interface,
                    _ => null
                },
                _ => null
            };
    }
}
