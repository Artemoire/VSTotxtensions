using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringLiteralOffsetAdornment
{
    public static class SyntaxNodeExtensions
    {

        public static SyntaxNode DeepestEnclosingDescendant(this SyntaxNode node, int position)
        {
            return node.DescendantNodes().Where(x => x.Span.Start <= position && x.Span.End > position).LastOrDefault();
        }

        public static SyntaxNode EnclosingDescendant<ISyntaxNode>(this SyntaxNode node, int position) where ISyntaxNode : SyntaxNode
        {
            return node.DescendantNodes().Where(x => x.Span.Start <= position && x.Span.End > position).LastOrDefault();
        }

    }
}
