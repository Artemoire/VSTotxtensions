using Microsoft.CodeAnalysis.CodeRefactorings;
using System.Threading.Tasks;

namespace TotRefactorings
{
    internal interface IRefactoringComputer
    {
        Task ComputeRefactoringsAsync(CodeRefactoringContext context);
    }
}