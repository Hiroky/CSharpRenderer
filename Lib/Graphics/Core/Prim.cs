using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using SlimDX;

namespace Lib
{
	/// <summary>
	/// プリミティブ描画クラス
	/// </summary>
	public class Prim : IDisposable
	{
		int vertexNum_ = 0;
		uint vertexAttr_ = 0;
		VertexBuffer vertexBuffer_ = null;
		Material material_ = null;
		Matrix worldMatrix_;

		public Matrix WorldMatrix { get { return worldMatrix_; } set { worldMatrix_ = value; } }
		public RenderState.PrimitiveTopology PrimTopology
		{
			get { return vertexBuffer_.Topology; }
			set { vertexBuffer_.Topology = value; }
		}

		/// <summary>
		/// 基本初期化
		/// </summary>
		public Prim(string shader_name, uint vertex_attr)
		{
			vertexAttr_ = vertex_attr;
			material_ = new Material();
			material_.SetShader(shader_name, vertex_attr);
			WorldMatrix = Matrix.Identity;
		}

		public Prim(uint shader_id, uint vertex_attr)
		{
			vertexAttr_ = vertex_attr;
			material_ = new Material();
			material_.SetShader(shader_id, vertex_attr);
			WorldMatrix = Matrix.Identity;
		}

		public Prim(Shader shader, uint vertex_attr)
		{
			vertexAttr_ = vertex_attr;
			material_ = new Material();
			material_.SetShader(shader);
			WorldMatrix = Matrix.Identity;
		}

		/// <summary>
		/// 頂点バッファ生成構造体から初期化
		/// </summary>
		/// <param name="buffer_desc"></param>
		public Prim(VertexBuffer.BufferDesc buffer_desc)
		{
			vertexNum_ = buffer_desc.data_size / buffer_desc.stride;

			vertexBuffer_ = new VertexBuffer(buffer_desc);
			WorldMatrix = Matrix.Identity;
		}

		/// <summary>
		/// 頂点バッファデータを共有して初期化
		/// </summary>
		/// <param name="buffer"></param>
		public Prim(Buffer buffer)
		{
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		public void Dispose()
		{
			if( vertexBuffer_ != null ) vertexBuffer_.Dispose();
		}


		/// <summary>
		/// 矩形を追加する
		/// </summary>
		/// <param name="rect"></param>
		public void AddRect(ref Rect rect)
		{
			vertexNum_ = 6;

			// バッファデータ
			if ((vertexAttr_ & (uint)Shader.VertexAttr.TEXCOORD0) != 0) {
				// UV付き
				int vertex_stride = 20;
				int data_size = vertex_stride * vertexNum_;
				var vtx_ary = new[] {
					new { p = rect.leftTop, uv = new Vector2(0, 0) },
					new { p = rect.rightBottom, uv = new Vector2(1, 1) },
					new { p = new Vector3(rect.rightBottom.X, rect.leftTop.Y, rect.leftTop.Z), uv = new Vector2(1, 0) },
					new { p = rect.leftTop, uv = new Vector2(0, 0) },
					new { p = new Vector3(rect.leftTop.X, rect.rightBottom.Y, rect.leftTop.Z), uv = new Vector2(0, 1) },
					new { p = rect.rightBottom, uv = new Vector2(1, 1) },
				};
				var vertices = new DataStream(data_size, true, true);
				foreach (var a in vtx_ary) {
					vertices.Write(a.p);
					vertices.Write(a.uv);
				}
				vertices.Position = 0;

				// 頂点バッファ生成
				VertexBuffer.BufferDesc desc = new VertexBuffer.BufferDesc() { data = vertices, data_size = data_size, stride = vertex_stride };
				vertexBuffer_ = new VertexBuffer(desc);
				vertices.Close();
			} else {
				// positionのみ
				int vertex_stride = 12;
				int data_size = vertex_stride * vertexNum_;
				var vertices = new DataStream(data_size, true, true);
				vertices.Write(rect.leftTop);
				vertices.Write(rect.rightBottom);
				vertices.Write(new Vector3(rect.rightBottom.X, rect.leftTop.Y, rect.leftTop.Z));
				vertices.Write(rect.leftTop);
				vertices.Write(new Vector3(rect.leftTop.X, rect.rightBottom.Y, rect.leftTop.Z));
				vertices.Write(rect.rightBottom);
				vertices.Position = 0;

				// 頂点バッファ生成
				VertexBuffer.BufferDesc desc = new VertexBuffer.BufferDesc() { data = vertices, data_size = data_size, stride = vertex_stride };
				vertexBuffer_ = new VertexBuffer(desc);
				vertices.Close();
			}
		}

		public void AddVertex(Vector3[] vertices)
		{
			vertexNum_ = vertices.Length;

			// positionのみ
			int vertex_stride = 12;
			int data_size = vertex_stride * vertexNum_;
			var stream = new DataStream(data_size, true, true);
			foreach (var v in vertices) {
				stream.Write(v);
			}
			stream.Position = 0;

			// 頂点バッファ生成
			VertexBuffer.BufferDesc desc = new VertexBuffer.BufferDesc() { data = stream, data_size = data_size, stride = vertex_stride };
			vertexBuffer_ = new VertexBuffer(desc);
			stream.Close();
		}

		/// <summary>
		/// 描画
		/// </summary>
		public void Draw(GraphicsContext context, int count = 1)
		{
			// 頂点バッファバインド
			context.SetVertexBuffer(0, vertexBuffer_);

			// マテリアル
			material_.Setup(context);
			ShaderManager.SetUniformParams(ref worldMatrix_);

			// 描画
			if (count > 1) {
				context.DrawInstanced(0, vertexNum_, 0, count);
			} else {
				context.Draw(0, vertexNum_);
			}
		}


		/// <summary>
		/// マテリアル取得
		/// </summary>
		/// <returns></returns>
		public Material GetMaterial() 
		{
			return material_;
		}
	}
}
