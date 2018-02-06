using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace CopyCtorCommand
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CopyCtorCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("5f6a157f-eacd-4256-9857-1ca3ba926a5d");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyCtorCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private CopyCtorCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CopyCtorCommand Instance {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider {
            get {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new CopyCtorCommand(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            var documentFilePath = dte?.ActiveDocument?.FullName;
            if (documentFilePath == null)
                return;
            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            Workspace workspace = componentModel.GetService<VisualStudioWorkspace>();
            var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(documentFilePath).FirstOrDefault();
            if (documentId == null)
                return;

            var document = workspace.CurrentSolution.GetDocument(documentId);

            Microsoft.VisualStudio.Text.Editor.IWpfTextView textView = Helpers.GetCurentTextView();
            int position = textView.Caret.Position.BufferPosition.Position;
            var root = document.GetSyntaxRootAsync().Result;
            var model = document.GetSemanticModelAsync().Result;

            var classSyntaxNode = GetSurroundingSyntaxNode<ClassDeclarationSyntax>(root, position);
            if (classSyntaxNode == null)
                return;
            var ctor = GetSurroundingSyntaxNode<ConstructorDeclarationSyntax>(root, position);
            if (ctor == null)
                return;
            var classSymbol = model.GetDeclaredSymbol(classSyntaxNode);
            var sourceProperties = classSymbol.GetMembers().OfType<IPropertySymbol>()
                .Where(x => x.DeclaredAccessibility == Accessibility.Public);
            var sourcePropertiesNames = sourceProperties
                .Select(x => x.Name);
            IEnumerable<IPropertySymbol> targetProperties = null;
            IEnumerable<string> targetPropertiesNames = null;
            string targetName = "";
            foreach (var parameter in ctor.ParameterList.Parameters)
            {
                if (parameter.Type is IdentifierNameSyntax)
                {
                    targetName = parameter.Identifier.ValueText;
                    targetProperties = ((ITypeSymbol)model.GetSymbolInfo(parameter.Type).Symbol)
                        .GetMembers().OfType<IPropertySymbol>()
                        .Where(x => x.DeclaredAccessibility == Accessibility.Public);
                    targetPropertiesNames = targetProperties.Select(x => x.Name);
                    if (ClassPropertiesCompatible(sourcePropertiesNames, targetPropertiesNames))
                        break;
                }
            }
            if (targetProperties.Count() == 0)
                return;
            targetPropertiesNames = targetProperties.Select(x => x.Name);
            var targetPropertiesNamesList = targetPropertiesNames.ToList();
            String builder = "";
            var targetIdx = -1;
            foreach (var property in sourceProperties)
            {
                var propertyName = property.Name;

                builder += propertyName + " = ";
                if ((targetIdx = targetPropertiesNamesList.IndexOf(propertyName)) > -1)
                    builder += targetName + "." + targetPropertiesNamesList[targetIdx];
                else
                    builder += TypeDefaultValue(property);
                builder += ";" + Environment.NewLine;

            }
            builder = builder.Substring(0, builder.Length - Environment.NewLine.Length);
            TextSelection s = (TextSelection)dte.ActiveDocument.Selection;
            VirtualPoint vp = s.AnchorPoint;
            EditPoint p = vp.CreateEditPoint();
            p.Insert(builder);
            p.SmartFormat(vp);
            //using (var edit = textView.TextSnapshot.TextBuffer.CreateEdit())
            //{
            //    edit.Insert(position, builder);
            //    var snap = edit.Apply();
            //}
        }

        public static TSyntaxNode GetSurroundingSyntaxNode<TSyntaxNode>(SyntaxNode root, int position) where TSyntaxNode : SyntaxNode
        {
            try
            {
                return root.DescendantNodes()
                .OfType<TSyntaxNode>()
                .Where(x => x.Span.End >= position && x.Span.Start <= position)
                .FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }

        }

        public static bool ClassPropertiesCompatible(IEnumerable<string> sourcePropertiesNames, IEnumerable<string> targetPropertiesNames)
        {
            foreach (var property in sourcePropertiesNames)
            {
                if (targetPropertiesNames.Contains(property))
                    return true;
            }
            return false;
        }

        public static string TypeDefaultValue(IPropertySymbol property)
        {
            var type = property.Type;
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
                    return "0";
                case SpecialType.System_Boolean:
                    return "false";
                case SpecialType.System_Char:
                    return "'0'";
                case SpecialType.System_String:
                    return "\"\"";
            }
            switch (type.TypeKind)
            {
                case TypeKind.Class:
                    return "null";
            }
            return "";
        }
    }
}
