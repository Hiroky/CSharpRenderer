using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;
using SlimDX.Direct3D11;

namespace Lib
{
	public static class CubemapHelper
	{
		/// <summary>
		/// キューブマップにレンダリング
		/// </summary>
		public static void RenderingCubeMap(GraphicsContext context, Texture cubemap, Texture depth, Vector3 position, Action sceneDrawFunc)
		{
			GraphicsCore.FrameBuffer frameBuffer = new GraphicsCore.FrameBuffer() {
				color_buffer_ = new Texture[1] { cubemap },
				depth_stencil_ = depth,
				is_array_buffer_ = true,
			};

			Camera backupCamera = GraphicsCore.CurrentDrawCamera;
			Camera camera = new Camera();
			// 右手対応のためx反転している
			Matrix projection = Matrix.PerspectiveFovRH((float)System.Math.PI / 2.0f, 1.0f, 0.1f, 1000.0f) * Matrix.Scaling(-1, 1, 1);
			Matrix view = Matrix.LookAtRH(position, Vector3.Zero, Vector3.UnitZ);
			camera.InitializeExternal(view, projection);
			GraphicsCore.CurrentDrawCamera = camera;
			GraphicsCore.SetRasterizerState(RenderState.RasterizerState.CullFront);

			// 各面をレンダリング
			for (int i = 0; i < 6; ++i) {
				Vector3 lookAt = new Vector3();
				Vector3 upVec = new Vector3();
				switch (i) {
					case 0:
						lookAt = new Vector3(1.0f, 0.0f, 0.0f);
						upVec = new Vector3(0.0f, 1.0f, 0.0f);
						break;
					case 1:
						lookAt = new Vector3(-1.0f, 0.0f, 0.0f);
						upVec = new Vector3(0.0f, 1.0f, 0.0f);
						break;
					case 2:
						lookAt = new Vector3(0.0f, 1.0f, 0.0f);
						upVec = new Vector3(0.0f, 0.0f, -1.0f);
						break;
					case 3:
						lookAt = new Vector3(0.0f, -1.0f, 0.0f);
						upVec = new Vector3(0.0f, 0.0f, 1.0f);
						break;
					case 4:
						lookAt = new Vector3(0.0f, 0.0f, 1.0f);
						upVec = new Vector3(0.0f, 1.0f, 0.0f);
						break;
					case 5:
						lookAt = new Vector3(0.0f, 0.0f, -1.0f);
						upVec = new Vector3(0.0f, 1.0f, 0.0f);
						break;
				}

				frameBuffer.array_buffer_index_ = i;
				view = Matrix.LookAtRH(position, position + lookAt, upVec);
				camera.SetViewMatrix(ref view);
				camera.Update();

				// TODO:要対応
				//GraphicsCore.BeginRender(frameBuffer);
				//sceneDrawFunc();
				//GraphicsCore.EndRender();
			}
			GraphicsCore.CurrentDrawCamera = backupCamera;
			GraphicsCore.SetRasterizerState(RenderState.RasterizerState.CullBack);
		}



		/// <summary>
		/// PBR用プレフィルタキューブマップを作成する
		/// </summary>
		/// <param name="output"></param>
		/// <param name="source"></param>
		public static void PrefilterRadiance(Texture output, Texture source)
		{
			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			var prim = new Prim("PrefilterRadianceMap", (uint)Shader.VertexAttr.TEXCOORD0);
			prim.AddRect(ref rect);
			prim.GetMaterial().DepthState = RenderState.DepthState.None;
			prim.GetMaterial().SetShaderViewPS(0, source);

			int miplevel = 1;
			int size = source.Width;
			while (size > 1) {
				size = size >> 1;
				miplevel++;
			}

			// テクスチャを再生成
			output.Dispose();
			output.Initialize(new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				optionFlags = ResourceOptionFlags.TextureCube,
				width = source.Width,
				height = source.Height,
				mips = miplevel,
				format = SlimDX.DXGI.Format.R16G16B16A16_Float,
			});
			GraphicsCore.FrameBuffer frameBuffer = new GraphicsCore.FrameBuffer();
			frameBuffer.color_buffer_ = new Texture[1] { output };
			frameBuffer.is_array_buffer_ = true;

			// 描画
			for(int m = 0; m < miplevel; m++){
				for (int i = 0; i < 6; i++) {
					// コンスタントバッファ
					Shader.SetConstantBufferUpdateFunc("CB_PrefilterCubeMap", (s, inst) => {
						dynamic cb = inst;
						cb.g_face = (float)i;
						cb.g_mipLevel = (float)m;
						cb.g_maxMipLevel = (float)miplevel;
						return true;
					});
					frameBuffer.array_buffer_index_ = m * 6 + i;
					// TODO:
					//GraphicsCore.BeginRender(frameBuffer);
					//prim.Draw();
					//GraphicsCore.EndRender();
				}
			}

			prim.Dispose();
		}


		/// <summary>
		/// BRDFで使用するLUTを生成する(UE4方式)
		/// </summary>
		public static Texture CreateBRDFLookUpTable(GraphicsContext context, int w, int h)
		{
			var tex = new Texture(new Texture.InitDesc {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				width = w,
				height = h,
				format = SlimDX.DXGI.Format.R16G16_Float,
			});
			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			var prim = new Prim("GenerateBRDF_LUT", (uint)Shader.VertexAttr.TEXCOORD0);
			prim.AddRect(ref rect);
			prim.GetMaterial().DepthState = RenderState.DepthState.None;

			GraphicsCore.FrameBuffer frameBuffer = new GraphicsCore.FrameBuffer();
			frameBuffer.color_buffer_ = new Texture[1] { tex };

			context.SetRenderTargets(frameBuffer.color_buffer_, null);
			prim.Draw();
			prim.Dispose();
			return tex;
		}
	}
}
