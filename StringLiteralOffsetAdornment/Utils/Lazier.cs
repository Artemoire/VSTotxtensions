using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;

namespace StringLiteralOffsetAdornment.Utils
{
    public class Lazier<T> where T : class
    {
        private Func<T> _lazierFactory;
        private T _value;
        public T Value { get { return IsValueCreated ? _value : TryInitializing(); } }
        public bool IsValueCreated { get; private set; }

        public Lazier(Func<T> lazierFactory)
        {
            _lazierFactory = lazierFactory;
        }

        private T TryInitializing()
        {
            _value = _lazierFactory();
            IsValueCreated = _value != null;
            return _value;
        }   
    }
}
