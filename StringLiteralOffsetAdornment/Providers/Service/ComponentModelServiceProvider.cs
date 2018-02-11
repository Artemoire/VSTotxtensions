using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using StringLiteralOffsetAdornment.Utils;

namespace StringLiteralOffsetAdornment.Providers.Service
{
    public class ComponentModelServiceProvider
    {
        private static Lazier<IComponentModel> _lazierComponentModel = new Lazier<IComponentModel>(ComponentModelProvider);
        public static IComponentModel ComponentModel { get { return _lazierComponentModel.Value; } }

        private static IComponentModel ComponentModelProvider()
        {
            return (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
        }
    }
}
