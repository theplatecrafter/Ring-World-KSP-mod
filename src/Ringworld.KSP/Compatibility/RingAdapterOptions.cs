namespace NivenRingworld
{
    internal static class RingAdapterOptions
    {
        internal static bool Enabled(string key)
        {
            bool result=true,value;
            if(GameDatabase.Instance!=null)
                foreach(var n in GameDatabase.Instance.GetConfigNodes("RINGWORLD_COMPATIBILITY"))
                    if(bool.TryParse(n.GetValue(key),out value))result=value;
            return result;
        }
    }
}
