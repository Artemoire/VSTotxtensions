using EnvDTE;
using Microsoft.VisualStudio.Shell;
using StringLiteralOffsetAdornment.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StringLiteralOffsetAdornment.Providers.Service
{
    public class DTEServiceProvider
    {
        private static Lazier<DTE> _lazierDTE = new Lazier<DTE>(DTEProvider);
        public static DTE DTE { get { return _lazierDTE.Value; } }

        private static DTE DTEProvider()
        {
            return Package.GetGlobalService(typeof(DTE)) as DTE;
        }
    }
}
