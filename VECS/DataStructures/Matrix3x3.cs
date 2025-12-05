namespace System.Numerics
{
    public  struct Matrix3x3
    {
        /// <summary>Column 0 of the matrix.</summary>
        public Vector3 c0;
        /// <summary>Column 1 of the matrix.</summary>
        public Vector3 c1;
        /// <summary>Column 2 of the matrix.</summary>
        public Vector3 c2;

        public static readonly Matrix3x3 identity = new(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);

        public Matrix3x3(float m00, float m01, float m02,
                        float m10, float m11, float m12,
                        float m20, float m21, float m22)
        {

            c0 = new Vector3(m00, m10, m20);
            c1 = new Vector3(m01, m11, m21);
            c2 = new Vector3(m02, m12, m22);
        }

        public Matrix3x3(Matrix4x4 f4x4)
        {
            c0 = f4x4.GetMatrixColumn(0).AsVector3();//.c0.xyz;
            c1 = f4x4.GetMatrixColumn(1).AsVector3();//.c1.xyz;
            c2 = f4x4.GetMatrixColumn(2).AsVector3();//.c2.xyz;
        }
    }
}
