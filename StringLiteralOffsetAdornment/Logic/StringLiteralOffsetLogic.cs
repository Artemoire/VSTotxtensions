using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringLiteralOffsetAdornment.Logic
{
    public class StringLiteralOffsetLogic
    {
        public static int CalcStringLiteralPosition(SyntaxNode root, int caretAbsoluteOffset)
        {
            var enclosing = root.DeepestEnclosingDescendant(caretAbsoluteOffset);
            if (enclosing != null && enclosing.IsKind(SyntaxKind.StringLiteralExpression))
            {
                int offset = root.ToFullString().IndexOf('"', enclosing.Span.Start) + 1;
                return caretAbsoluteOffset - offset;
            }

            return -1;
        }
    }
}
