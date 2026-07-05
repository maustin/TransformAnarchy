using System.Reflection;

namespace TransformAnarchy {

    public static class PatchUtils
    {

        public static object InvokeParamless(System.Type type, object instance, MethodBase method)
        {
            return method.Invoke(instance, new object[] { });
        }
    }
}
