using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace StringLiteralOffsetAdornment
{
    /// <summary>
    /// Establishes an <see cref="IAdornmentLayer"/> to place the adornment on and exports the <see cref="IWpfTextViewCreationListener"/>
    /// that instantiates the adornment on the event of a <see cref="IWpfTextView"/>'s creation
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class StringLiteralOffsetAdornmentTextViewCreationListener : IWpfTextViewCreationListener
    {
        // Disable "Field is never assigned to..." and "Field is never used" compiler's warnings. Justification: the field is used by MEF.
#pragma warning disable 649, 169

        /// <summary>
        /// Defines the adornment layer for the scarlet adornment. This layer is ordered
        /// after the selection layer in the Z-order
        /// </summary>
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("StringLiteralOffsetAdornment")]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        private AdornmentLayerDefinition editorAdornmentLayer;

#pragma warning restore 649, 169

        /// <summary>
        /// Instantiates a StringLiteralOffsetAdornment manager when a textView is created.
        /// </summary>
        /// <param name="textView">The <see cref="IWpfTextView"/> upon which the adornment should be placed</param>
        public void TextViewCreated(IWpfTextView textView)
        {
            Microsoft.CodeAnalysis.Document document = null;
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            var activeDocument = dte?.ActiveDocument;
            if (activeDocument != null)
            {
                var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                var workspace = (Microsoft.CodeAnalysis.Workspace)componentModel.GetService<VisualStudioWorkspace>();
                var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(activeDocument.FullName).FirstOrDefault();
                if (documentId != null)
                {
                    document = workspace.CurrentSolution.GetDocument(documentId);
                }
            }
            // The adorment will get wired to the text view events
            new StringLiteralOffsetAdornment(textView, document);
        }
    }
}
