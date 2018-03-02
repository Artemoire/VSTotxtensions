using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace TotRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(TotRefactoringsCodeRefactoringProvider)), Shared]
    internal class TotRefactoringsCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            await (new InitFieldFromConstructorRefactor()).ComputeRefactoringsAsync(context).ConfigureAwait(false);
            await (new CopyCtorRefactor()).ComputeRefactoringsAsync(context).ConfigureAwait(false);
        }
    }
}
