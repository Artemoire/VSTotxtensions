using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace TotRefactorings.Extensions
{
    public static class ConstructorDeclarationExtensions
    {
        public static bool IsBodyEmpty(this ConstructorDeclarationSyntax node) => node.Body?.Statements.Count() == 0;

        public static bool HasBody(this ConstructorDeclarationSyntax node) => node.Body != null;
    }
}
