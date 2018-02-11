using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using StringLiteralOffsetAdornment.Providers.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringLiteralOffsetAdornment.Helpers
{
    public class CodeAnalysisDocumentHelpers
    {
        public static Document TryGetDocument()
        {
            var dte = DTEServiceProvider.DTE;
            var activeDocument = dte?.ActiveDocument; // sometimes we're constructed/invoked before ActiveDocument has been set

            if (activeDocument == null)
                return null;

            var componentModel = ComponentModelServiceProvider.ComponentModel;            

            if (componentModel == null)
                return null;

            var workspace = componentModel.GetService<VisualStudioWorkspace>();

            //if (workspace == null) TODO: What to do when no workspace?
            //    return null;

            var documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(activeDocument.FullName).FirstOrDefault();

            if (documentId == null)
                return null;

            return workspace.CurrentSolution.GetDocument(documentId);
        }
    }
}
