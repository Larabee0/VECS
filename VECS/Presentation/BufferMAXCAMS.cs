using System.Runtime.CompilerServices;

namespace VECS
{
    [InlineArray(Presenter.MAX_POINT_LIGHTS)]
    public struct BufferMAXCAMS<T> where T : unmanaged
    {
        private T element0;
    }
}
