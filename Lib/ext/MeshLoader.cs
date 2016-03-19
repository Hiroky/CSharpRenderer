#define CALC_PARALLEL
#define USE_DIRECTX_MESH
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;

namespace Lib.Ext
{
	[Serializable]
	class MyVertex
	{
		public float x;
		public float y;
		public float z;

		public float nx;
		public float ny;
		public float nz;

		public float u;
		public float v;
	}

	class MyVertexBump : MyVertex
	{
		public float tx;
		public float ty;
		public float tz;

		public float btx;
		public float bty;
		public float btz;
	}

	class MaterialObject
	{
		public string ID;
		public List<string> textureID;

		public MaterialObject()
		{
			textureID = new List<string>();
		}
	}

	class DrawSubset
	{
		public string material;
		public int startIndex;
		public int endIndex;
	}


	abstract class MeshLoader
	{
		public List<MyVertex> m_objVertices;
		public List<MyVertex> m_Vertices;
		public List<Int32> m_Indices;
		public List<Vector3> m_Normals;
		public List<Vector2> m_TexCoords;

		public List<MaterialObject> m_materialList;
		public List<DrawSubset> m_subsetList;
		public Dictionary<string, string> m_textureList;

		public Vector3 m_BoundingBoxMin;
		public Vector3 m_BoundingBoxMax;

		public MeshLoader()
		{
			m_objVertices = new List<MyVertex>();
			m_Vertices = new List<MyVertex>();
			m_Indices = new List<Int32>();
			m_Normals = new List<Vector3>();
			m_TexCoords = new List<Vector2>();
			m_materialList = new List<MaterialObject>();
			m_subsetList = new List<DrawSubset>();
			m_textureList = new Dictionary<string, string>();
			m_BoundingBoxMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			m_BoundingBoxMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="calcTangent"></param>
		public abstract void Load(String fileName, bool calcTangent = false);


		/// <summary>
		/// tangent, binormalを計算する
		/// </summary>
		/// <param name="tangents3"></param>
		/// <param name="tangents4"></param>
		/// <param name="bitangents"></param>
		/// <returns></returns>
		public bool ComputeTangentFrame()
		{
			// TODO:高速化
			uint nVerts = (uint)m_Vertices.Count;
			uint nFaces = (uint)m_Indices.Count / 3;
			int[] indices = m_Indices.ToArray();
			Vector3[] positions = new Vector3[nVerts];
			Vector3[] normals = new Vector3[nVerts];
			Vector2[] texcoords = new Vector2[nVerts];
			Vector3[] tangents3 = new Vector3[nVerts];
			Vector3[] bitangents = new Vector3[nVerts];

#if CALC_PARALLEL
			Parallel.For(0, nVerts, idx => {
				int i = (int)idx;
#else
			for (int i = 0;i < nVerts; i++) {
#endif
				positions[i] = new Vector3(m_Vertices[i].x, m_Vertices[i].y, m_Vertices[i].z);
				normals[i] = new Vector3(m_Vertices[i].nx, m_Vertices[i].ny, m_Vertices[i].nz);
				texcoords[i] = new Vector2(m_Vertices[i].u, m_Vertices[i].v);
			}
#if CALC_PARALLEL
			);
#endif

			if ((UInt64)(nFaces * 3) >= UInt32.MaxValue)
				return false;

			const float EPSILON = 0.0001f;
			Vector4 s_flips = new Vector4(1.0f, -1.0f, -1.0f, 1.0f);	
			Vector4 g_XMIdentityR0 = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);
			Vector4 g_XMIdentityR1 = new Vector4(0.0f, 1.0f, 0.0f, 0.0f);
			Vector4 g_XMIdentityR2 = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);

			Vector4[] tangent1 = new Vector4[nVerts];
			Vector4[] tangent2 = new Vector4[nVerts];
			for (int i = 0; i < nVerts; i++) {
				tangent1[i] = Vector4.Zero;
				tangent2[i] = Vector4.Zero;
			}

#if CALC_PARALLEL
			Parallel.For(0, nFaces, idx => {
				int face = (int)idx;
#else
			for( int face = 0; face < nFaces; ++face ) {
#endif
				int i0 = indices[ face * 3 ];
				int i1 = indices[ face * 3 + 1 ];
				int i2 = indices[ face * 3 + 2 ];

#if USE_DIRECTX_MESH
				Vector4 t0 = new Vector4( texcoords[ i0 ], 0, 0 );
				Vector4 t1 = new Vector4( texcoords[ i1 ], 0, 0 );
				Vector4 t2 = new Vector4( texcoords[ i2 ], 0, 0 );
        
				Vector4 s = XMVectorMergeXY( t1 - t0, t2 - t0 );

				Vector4 tmp = s;

				float d = tmp.X * tmp.W - tmp.Z * tmp.Y;
				
				d = ( System.Math.Abs( d ) <= EPSILON ) ? 1.0f : ( 1.0f / d );
				s *= d;
				s = Vector4.Modulate(s, s_flips);

				Matrix m0 = new Matrix();
				m0.set_Rows(0, XMVectorPermute(new int[] { 3, 2, 6, 7 }, s, Vector4.Zero));
				m0.set_Rows(1, XMVectorPermute(new int[] { 1, 0, 4, 5 }, s, Vector4.Zero));
				m0.set_Rows(2, Vector4.Zero);
				m0.set_Rows(3, Vector4.Zero);

				Vector4 p0 = new Vector4( positions[ i0 ], 0 );
				Vector4 p1 = new Vector4( positions[ i1 ], 0 );
				Vector4 p2 = new Vector4( positions[ i2 ], 0 );

				Matrix m1 = new Matrix();
				m1.set_Rows(0, p1 - p0);
				m1.set_Rows(1, p2 - p0);
				m1.set_Rows(2, Vector4.Zero);
				m1.set_Rows(3, Vector4.Zero);

				Matrix uv = Matrix.Multiply( m0, m1 );

				tangent1[i0] = tangent1[i0] + uv.get_Rows(0);
				tangent1[i1] = tangent1[i1] + uv.get_Rows(0);
				tangent1[i2] = tangent1[i2] + uv.get_Rows(0);
				tangent2[i0] = tangent2[i0] + uv.get_Rows(1);
				tangent2[i1] = tangent2[i1] + uv.get_Rows(1);
				tangent2[i2] = tangent2[i2] + uv.get_Rows(1);
#else
				Vector3 v1 = positions[i0];
				Vector3 v2 = positions[i1];
				Vector3 v3 = positions[i2];

				Vector2 w1 = texcoords[i0];
				Vector2 w2 = texcoords[i1];
				Vector2 w3 = texcoords[i2];

				double x1 = v2.X - v1.X;
				double x2 = v3.X - v1.X;
				double y1 = v2.Y - v1.Y;
				double y2 = v3.Y - v1.Y;
				double z1 = v2.Z - v1.Z;
				double z2 = v3.Z - v1.Z;

				double s1 = w2.X - w1.X;
				double s2 = w3.X - w1.X;
				double t1 = w2.Y - w1.Y;
				double t2 = w3.Y - w1.Y;

				double r = 1.0F / (s1 * t2 - s2 * t1);
				Vector3 sdir = new Vector3(
					(float)(t2 * x1 - t1 * x2) * (float)r,
					(float)(t2 * y1 - t1 * y2) * (float)r,
					(float)(t2 * z1 - t1 * z2) * (float)r);
				Vector3 tdir = new Vector3(
					(float)(s1 * x2 - s2 * x1) * (float)r,
					(float)(s1 * y2 - s2 * y1) * (float)r,
					(float)(s1 * z2 - s2 * z1) * (float)r);

				tangent1[i0] += new Vector4(sdir, 0);
				tangent1[i1] += new Vector4(sdir, 0);
				tangent1[i2] += new Vector4(sdir, 0);

				tangent2[i0] += new Vector4(tdir, 0);
				tangent2[i1] += new Vector4(tdir, 0);
				tangent2[i2] += new Vector4(tdir, 0);
#endif
			}
#if CALC_PARALLEL
			);
#endif


#if CALC_PARALLEL
			Parallel.For(0, nVerts, idx => {
				int j = (int)idx;
#else
			for( int j = 0; j < nVerts; ++j ) {
#endif

#if USE_DIRECTX_MESH
				// Gram-Schmidt orthonormalization
				Vector4 b0 = new Vector4( normals[ j ], 0 );
				b0.Normalize();

				Vector4 tan1 = tangent1[ j ];
				Vector4 b1 = tan1 - XMVector3Dot(b0, tan1) * b0;
				b1.Normalize();

				Vector4 tan2 = tangent2[ j ];
				Vector4 b2 = tan2 - XMVector3Dot( b0, tan2 ) * b0 -  XMVector3Dot( b1, tan2 ) * b1;
				b2.Normalize();

				// handle degenerate vectors
				float len1 = XMVector3Length( b1 );
				float len2 = XMVector3Length( b2 );

				if ( ( len1 <= EPSILON ) || ( len2 <= EPSILON ) ) {
					if ( len1 > 0.5f ) {
						// Reset bi-tangent from tangent and normal
						b2 = XMVector3Cross( b0, b1 );
					} else if ( len2 > 0.5f ) {
						// Reset tangent from bi-tangent and normal
						b1 = XMVector3Cross( b2, b0 );
					} else {
						// Reset both tangent and bi-tangent from normal
						Vector4 axis;

						float d0 = System.Math.Abs(XMVector3Dot(g_XMIdentityR0, b0));
						float d1 = System.Math.Abs(XMVector3Dot(g_XMIdentityR1, b0));
						float d2 = System.Math.Abs(XMVector3Dot(g_XMIdentityR2, b0));
						if ( d0 < d1 ) {
							axis = ( d0 < d2 ) ? g_XMIdentityR0 : g_XMIdentityR2;
						} else if ( d1 < d2 ) {
							axis = g_XMIdentityR1;
						} else {
							axis = g_XMIdentityR2;
						}

						b1 = XMVector3Cross( b0, axis );
						b2 = XMVector3Cross( b0, b1 );
					}
				}
				tangents3[j] = new Vector3(b1.X, b1.Y, b1.Z);
				bitangents[j] = new Vector3(b2.X, b2.Y, b2.Z);

#if false
				if ( tangents4 != null ) {
					Vector4 bi = XMVector3Cross( b0, tan1 );
					float w = XMVector3Less( XMVector3Dot( bi, tan2 ), g_XMZero ) ? -1.f : 1.f;

					bi = XMVectorSetW( b1, w );
					XMStoreFloat4( &tangents4[ j ], bi );
				}
#endif

#else
				Vector4 n = new Vector4( normals[ j ], 0 );
				Vector4 t = tangent1[ j ];

				Vector4 tmp = (t - n * XMVector3Dot(n, t));
				tmp.Normalize();
				tangents3[j] = new Vector3(tmp.X, tmp.Y, tmp.Z);

				float w = (XMVector3Dot(XMVector3Cross(n, t), tangent2[j]) < 0.0f) ? -1.0f : 1.0f;
#endif
			}
#if CALC_PARALLEL
			);
#endif

			// 新規頂点を作成する
#if CALC_PARALLEL
			Parallel.For(0, nVerts, idx => {
				int i = (int)idx;
#else
			for( int i = 0; i < nVerts; ++i ) {
#endif
				var vt = new MyVertexBump();
				vt.x = m_Vertices[i].x;
				vt.y = m_Vertices[i].y;
				vt.z = m_Vertices[i].z;
				vt.nx = m_Vertices[i].nx;
				vt.ny = m_Vertices[i].ny;
				vt.nz = m_Vertices[i].nz;
				vt.u = m_Vertices[i].u;
				vt.v = m_Vertices[i].v;
				vt.tx = tangents3[i].X;
				vt.ty = tangents3[i].Y;
				vt.tz = tangents3[i].Z;
				vt.btx = bitangents[i].X;
				vt.bty = bitangents[i].Y;
				vt.btz = bitangents[i].Z;
				m_Vertices[i] = vt;
			}
#if CALC_PARALLEL
);
#endif

			return true;
		}


		float XMVector3Dot(Vector4 a, Vector4 b)
		{
			return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
		}

		float XMVector3Length(Vector4 a)
		{
			float dot = a.X * a.X + a.Y * a.Y + a.Z * a.Z;
			return (float)System.Math.Sqrt(dot);
		}

		Vector4 XMVector3Cross(Vector4 V1, Vector4 V2)
		{
			Vector4 vResult = new Vector4(
				(V1[1] * V2[2]) - (V1[2] * V2[1]),
				(V1[2] * V2[0]) - (V1[0] * V2[2]),
				(V1[0] * V2[1]) - (V1[1] * V2[0]),
				0.0f
			);
			return vResult;
		}

		Vector4 XMVectorPermute(int[] roc, Vector4 a, Vector4 b)
		{
			Vector4 r = new Vector4();
			for (int i = 0; i < 4; i++) {
				if (roc[i] > 3) {
					r[i] = b[roc[i] - 4];
				} else {
					r[i] = a[roc[i]];
				}
			}
			return r;
		}

		Vector4 XMVectorMergeXY(Vector4 a, Vector4 b)
		{
			Vector4 Result = new Vector4();
			Result[0] = a[0];
			Result[1] = b[0];
			Result[2] = a[1];
			Result[3] = b[1];
			return Result;
		}
	}
}
