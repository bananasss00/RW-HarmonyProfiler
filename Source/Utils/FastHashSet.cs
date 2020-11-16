using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;

namespace HarmonyProfiler
{
    public class FastHashSet<T>
    {
        private HashSet<T> _hashSet = new HashSet<T>();
        private List<T> _buffer = new List<T>();

        public void Add(T element) => _buffer.Add(element);

        public void RemoveFromHashSet(T element) => _hashSet.Remove(element);

        public void ClearHashSet() => _hashSet.Clear();

        public void AppendBuffer()
        {
            _hashSet.UnionWith(_buffer.Distinct());
            _buffer.Clear();
        }
    }
}