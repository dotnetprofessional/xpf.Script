namespace xpf.Scripting.SQLServer
{
    public interface IPersistType<T>
    {
        void Persist(object parent, T typeInstance, IdentityMap identityMap);
    }
}
