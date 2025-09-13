namespace VECS
{
    /// <summary>
    /// Allows the assembly to override the application entry point leaving it to handle the whole life cycle of <see cref="Application"/>
    /// If an assembly contains this, <see cref="Application"/> won't be constructed by the VECS assembly at all.
    /// 
    /// VECS will have loaded all the other assemblies however.
    /// 
    /// If multiple assemblies define an entry point then an error will be raised.
    /// 
    /// VECS contains a default <see cref="ISubAssemblyEntryPoint"/> used if no assembly defines one.
    /// </summary>
    internal interface ISubAssemblyEntryPoint
    {
        public void Main(string[] args);
    }
}
