namespace VECS.ECS
{
    /// <summary>
    /// interface defines a component. Component implmenetation must define a static ComponentId
    /// This should be marked abstract when generating new components otherwise this is not enforced.
    /// </summary>
    public interface IComponent
    {
        public static int ComponentId { get; }
        public int Id {  get; }
    }
}
