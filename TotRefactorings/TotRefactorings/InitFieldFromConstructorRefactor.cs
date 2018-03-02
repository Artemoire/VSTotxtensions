using System;
using System.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;
using TotRefactorings.Extensions;

namespace TotRefactorings
{
    internal class InitFieldFromConstructorRefactor : IRefactoringComputer
    {
        public async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var fieldDecl = root.EnclosingDescendant<FieldDeclarationSyntax>(context.Span.Start);

            if (fieldDecl == null) // Don-t do nothing if not field declaration on caret
                return;

            var oldClass = root.EnclosingDescendant<ClassDeclarationSyntax>(context.Span.Start);
            var oldCtor = oldClass.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();

            if (oldCtor == null) // Ctor gen already exists
                return;

            var declr8r = fieldDecl.DescendantNodes().OfType<VariableDeclaratorSyntax>().SingleOrDefault();
            var declType = fieldDecl.DescendantNodes().OfType<IdentifierNameSyntax>().SingleOrDefault();

            if (declType == null) // Primitive types aren-t interfaces
                return;

            var declTypeText = declType.Identifier.Text;

            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var declInfo = model.GetDeclaredSymbol(declr8r) as IFieldSymbol;

            if (declInfo.Type.BaseType != null) // Don-t do anything if not interface
                return;

            var name = declr8r.Identifier.Text.StartsWith("_") ? declr8r.Identifier.Text.Substring(1) : declr8r.Identifier.Text;

            if (CtorContainsNamedParam(name, oldCtor)) // Don-t to anything if field name exists in ctor params
                return;


            var targetName = name;
            var sourceName = (declr8r.Identifier.Text.StartsWith("_")) ? "_" + name : "this." + name;
            var targetTypeName = declTypeText;

            context.RegisterRefactoring(CodeAction.Create(
                "Initialize field:'" + declr8r.Identifier.Text + "' from constructor",
                ct => CreateDocumentChangesAsync(context, oldClass, oldCtor, targetTypeName, targetName, sourceName, ct)
                ));
        }

        public async Task<Document> CreateDocumentChangesAsync(CodeRefactoringContext context,
            ClassDeclarationSyntax oldClass,
            ConstructorDeclarationSyntax oldCtor,
            string targetTypeName, string targetName, string sourceName,
            CancellationToken cancellationToken)
        {
            var newParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(targetName))
                .WithType(SyntaxFactory.IdentifierName(targetTypeName))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newCtor = oldCtor.WithParameterList(oldCtor.ParameterList.AddParameters(newParam))
                .WithBody(oldCtor.Body.AddStatements(
                 SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName(sourceName),
                                SyntaxFactory.IdentifierName(targetName)))))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newClass = oldClass.ReplaceNode(oldCtor, newCtor);

            var oldRoot = await context.Document
                               .GetSyntaxRootAsync(cancellationToken)
                               .ConfigureAwait(false);

            return context.Document.WithSyntaxRoot(oldRoot.ReplaceNode(oldClass, newClass));
        }

        private bool CtorContainsNamedParam(string name, ConstructorDeclarationSyntax ctor)
        {
            var lowerName = name.ToLowerInvariant();
            return ctor.ParameterList.Parameters.Where(x => x.Identifier.Text.ToLowerInvariant().Equals(lowerName)).Any();
        }
    }
}
