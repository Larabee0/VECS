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

        //
        // Summary:
        //     The first element of the first row.
        public float M11 {  readonly get => c0.X; set => c0.X = value; }
        //
        // Summary:
        //     The second element of the first row.
        public float M12 {  readonly get => c1.X; set => c1.X = value; }
        //
        // Summary:
        //     The third element of the first row.
        public float M13 {  readonly get => c2.X; set => c2.X = value; }
        //
        // Summary:
        //     The first element of the second row.
        public float M21 {  readonly get => c0.Y; set => c0.Y = value; }
        //
        // Summary:
        //     The second element of the second row.
        public float M22 {  readonly get => c1.Y; set => c1.Y = value; }
        //
        // Summary:
        //     The third element of the second row.
        public float M23 {  readonly get => c2.Y; set => c2.Y = value; }
        //
        // Summary:
        //     The first element of the third row.
        public float M31 {  readonly get => c0.Z; set => c0.Z = value; }
        //
        // Summary:
        //     The second element of the third row.
        public float M32 {  readonly get => c1.Z; set => c1.Z = value; }
        //
        // Summary:
        //     The third element of the third row.
        public float M33 {  readonly get => c2.Z; set => c2.Z = value; }

        public static readonly Matrix3x3 identity = new(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);

        public Matrix3x3(float m00, float m01, float m02,
                        float m10, float m11, float m12,
                        float m20, float m21, float m22)
        {

            c0 = new Vector3(m00, m10, m20);
            c1 = new Vector3(m01, m11, m21);
            c2 = new Vector3(m02, m12, m22);
        }

        public Matrix3x3(Vector3 c0, Vector3 c1, Vector3 c2)
        {
            this.c0 = c0;
            this.c1 = c1;
            this.c2 = c2;
        }

        public Matrix3x3(Matrix4x4 f4x4)
        {
            c0 = f4x4.GetMatrixColumn(0).AsVector3();//.c0.xyz;
            c1 = f4x4.GetMatrixColumn(1).AsVector3();//.c1.xyz;
            c2 = f4x4.GetMatrixColumn(2).AsVector3();//.c2.xyz;
        }
    }
}
