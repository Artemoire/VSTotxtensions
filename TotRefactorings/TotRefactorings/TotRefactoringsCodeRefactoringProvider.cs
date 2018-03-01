using System;
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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using TotRefactorings.Extensions;

namespace TotRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(TotRefactoringsCodeRefactoringProvider)), Shared]
    internal class TotRefactoringsCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var ctor = root.EnclosingDescendant<ConstructorDeclarationSyntax>(context.Span.Start);
            var @class = root.EnclosingDescendant<ClassDeclarationSyntax>(context.Span.Start);
            if (ctor == null)
                return;
            if (!ctor.HasBody() || !ctor.IsBodyEmpty())
                return;
            if (ctor.ParameterList.Parameters.Count() != 1)
                return;
            var paramSyntax = ctor.ParameterList.Parameters[0];
            var idSyntax = paramSyntax.DescendantNodes().OfType<IdentifierNameSyntax>().SingleOrDefault();
            if (idSyntax == null)
                return;

            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var copy = (model.GetSymbolInfo(idSyntax).Symbol as INamedTypeSymbol);
            //var fields = copy.GetMembers().OfType<IFieldSymbol>().Where(x => x.AssociatedSymbol == null && x.CanBeReferencedByName && x.DeclaredAccessibility==Accessibility.Public);
            var copyProps = copy.GetMembers()
                .OfType<IPropertySymbol>().Where(x => x.CanBeReferencedByName == true && x.DeclaredAccessibility == Accessibility.Public);
            var props = model.GetDeclaredSymbol(@class).GetMembers()
                .OfType<IPropertySymbol>().Where(x => x.CanBeReferencedByName == true && x.DeclaredAccessibility == Accessibility.Public);

            if (props.Count() == 0)
            {
                var properties = CopyPropertyDeclarations(copyProps);
                var statements = CreateCopiedPropertyInitializers(properties, paramSyntax.Identifier.Text);
                var newClass = ModifyClass(context, @class, ctor, statements, properties);
                RegisterCopyWithProperties(context, @class, newClass, paramSyntax.Identifier.Text);
            }
            else
            {
                var statements = CreateCompatiblePropertyInitializers(props, copyProps, paramSyntax.Identifier.Text, out bool any);
                if (!any)
                    return;
                var newClass = ModifyClass(context, @class, ctor, statements, null);                
                RegisterCopy(context, @class, newClass);
            }
        }

        private void RegisterCopy(CodeRefactoringContext context, ClassDeclarationSyntax @class, ClassDeclarationSyntax newClass)
        {
            var action = CodeAction.Create(
                "Make Copy Constructor",
                ct =>
                ReplaceClassDocument(context, @class, newClass, ct)
                );
            context.RegisterRefactoring(action);
        }

        private void RegisterCopyWithProperties(CodeRefactoringContext context, ClassDeclarationSyntax @class, ClassDeclarationSyntax newClass, string paramName)
        {
            var action = CodeAction.Create(
                "Copy Properties And Values From '"+paramName+"'",
                ct =>
                ReplaceClassDocument(context, @class, newClass, ct)
                );
            context.RegisterRefactoring(action);
        }

        private ClassDeclarationSyntax ModifyClass(CodeRefactoringContext context,
            ClassDeclarationSyntax @class,
            ConstructorDeclarationSyntax ctor,
            IEnumerable<StatementSyntax> statements,
            IEnumerable<PropertyDeclarationSyntax> properties)
        {
            var newCtor = ctor.WithBody(SyntaxFactory.Block(statements.ToArray()));

            var newClass = @class.ReplaceNode(ctor, newCtor);

            if (properties != null)
            {
                foreach (var newProp in properties)
                {
                    newClass = newClass.WithMembers(newClass.Members.Insert(0, newProp));
                }
            }

            return newClass;
        }

        private async Task<Document> ReplaceClassDocument(CodeRefactoringContext context,
            ClassDeclarationSyntax oldClass,
            ClassDeclarationSyntax newClass,
            CancellationToken cancellationToken)
        {

            var oldRoot = await context.Document
                               .GetSyntaxRootAsync(cancellationToken)
                               .ConfigureAwait(false);

            return context.Document.WithSyntaxRoot(oldRoot.ReplaceNode(oldClass, newClass));
        }        

        private IEnumerable<PropertyDeclarationSyntax> CopyPropertyDeclarations(IEnumerable<IPropertySymbol> copyProps)
        {
            foreach (var prop in copyProps)
            {
                yield return
                        SyntaxFactory.PropertyDeclaration(
                            SyntaxFactory.IdentifierName(prop.Type.ToString()), prop.Name
                        ).WithAccessorList(SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new List<AccessorDeclarationSyntax> {
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            }
                            )))
                            .WithModifiers(SyntaxFactory.TokenList(
                                SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                            .WithAdditionalAnnotations(Formatter.Annotation);
            }
        }

        private IEnumerable<StatementSyntax> CreateCopiedPropertyInitializers(IEnumerable<PropertyDeclarationSyntax> props, string paramName)
        {
            foreach (var prop in props)
                yield return SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName(prop.Identifier.Text),
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(paramName),
                                        SyntaxFactory.IdentifierName(prop.Identifier.Text))));
        }

        private IEnumerable<StatementSyntax> CreateCompatiblePropertyInitializers(IEnumerable<IPropertySymbol> props,
            IEnumerable<IPropertySymbol> copyProps, string paramName, out bool any)
        {
            any = false;
            var statements = new List<StatementSyntax>(props.Count());
            foreach (var prop in props)
            {
                ExpressionSyntax assignValue;
                if (copyProps.Where(x => x.Name.Equals(prop.Name) && x.Type.Equals(prop.Type)).Any())
                {
                    assignValue = SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(paramName),
                                    SyntaxFactory.IdentifierName(prop.Name));
                    any = true;
                }
                else
                    assignValue = TypeDefaultValue(prop.Type);
                statements.Add(SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    SyntaxFactory.IdentifierName(prop.Name),
                                    assignValue)));
            }
            return statements;
        }

        //private async Task<Document> TryCreateCopyConstructorAsync(CodeRefactoringContext context,
        //    ConstructorDeclarationSyntax ctor,
        //    ClassDeclarationSyntax @class,
        //    ParameterSyntax paramSyntax,
        //    IdentifierNameSyntax copyIdSyntax, CancellationToken cancellationToken)
        //{

        //    if (props.Count() == 0) // TODO declare properties aswell
        //    {
        //        newProperties = new List<PropertyDeclarationSyntax>(copyProps.Count());
        //        foreach (var copyProp in copyProps)
        //        {
        //            newProperties.Add();
        //            newCtor = newCtor.WithBody(newCtor.Body.AddStatements(
        //                SyntaxFactory.ExpressionStatement(
        //                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
        //                    SyntaxFactory.IdentifierName(copyProp.Name),
        //                    SyntaxFactory.MemberAccessExpression(
        //                            SyntaxKind.SimpleMemberAccessExpression,
        //                            SyntaxFactory.IdentifierName((copyIdSyntax.Parent as ParameterSyntax).Identifier),
        //                            SyntaxFactory.IdentifierName(copyProp.Name)))))
        //                );
        //        }
        //    }
        //    foreach (var prop in props)
        //    {
        //        ExpressionSyntax assignValue;
        //        if (copyProps.Where(x => x.Name.Equals(prop.Name) && x.Type.Equals(prop.Type)).Any())
        //            assignValue = SyntaxFactory.MemberAccessExpression(
        //                            SyntaxKind.SimpleMemberAccessExpression,
        //                            SyntaxFactory.IdentifierName((copyIdSyntax.Parent as ParameterSyntax).Identifier),
        //                            SyntaxFactory.IdentifierName(prop.Name));



        //        else
        //            assignValue = TypeDefaultValue(prop.Type);
        //        newCtor = newCtor.WithBody(newCtor.Body.AddStatements(
        //                SyntaxFactory.ExpressionStatement(
        //                    SyntaxFactory.AssignmentExpression(
        //                        SyntaxKind.SimpleAssignmentExpression,
        //                        SyntaxFactory.IdentifierName(prop.Name),
        //                        assignValue))
        //                ).WithAdditionalAnnotations(Formatter.Annotation));
        //    }
        //    var newClass = @class.ReplaceNode(ctor, newCtor);
        //    if (newProperties != null)
        //    {
        //        foreach (var newProp in newProperties)
        //        {
        //            newClass = newClass.WithMembers(newClass.Members.Insert(0, newProp));
        //        }
        //    }
        //    var oldRoot = await context.Document
        //                       .GetSyntaxRootAsync(cancellationToken)
        //                       .ConfigureAwait(false);
        //    var newRoot = oldRoot.ReplaceNode(@class, newClass);

        //    return context.Document.WithSyntaxRoot(newRoot);
        //}

        private static ExpressionSyntax TypeDefaultValue(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_Byte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                        SyntaxFactory.Literal(0));
                case SpecialType.System_Boolean:
                    return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression,
                        SyntaxFactory.Token(SyntaxKind.FalseKeyword));
                case SpecialType.System_Char:
                    return SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression,
                        SyntaxFactory.Literal('0'));
                case SpecialType.System_String:
                    return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(""));
            }
            switch (type.TypeKind)
            {
                case TypeKind.Class:
                    return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression,
                        SyntaxFactory.Token(SyntaxKind.NullKeyword));
            }
            return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression,
                        SyntaxFactory.Token(SyntaxKind.NullKeyword));
        }
    }
}
