using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using SlimDX.DXGI;

namespace Lib
{
	public class ShadowMap : IDisposable
	{
		public enum Type
		{
			VSM,
			EVSM,
		}

		Type type_;
		Texture shadowMap_;
		Texture[] blurMap_;
		Renderer.FrameBuffer frameBuffer_;
		Renderer.FrameBuffer blurFrameBuffer_;
		Prim blurPrim_;
		Camera camera_;
		Func<Shader, Shader> shadowMapDrawShaderFunc_;
		Shader shadowCastShader_;

		string[] shaderName_;
		Format[] bufferFormat_;

		public Texture BaseMap { get { return shadowMap_; } }
		public Texture Map { get { return blurMap_[0]; } }
		public Camera Camera { get { return camera_; } }

		/// <summary>
		/// 
		/// </summary>
		public ShadowMap(int width, int height, Type type = Type.VSM)
		{
			type_ = type;
			shaderName_ = new string[] { "ConstructVSM", "ConstructEVSM" };
			bufferFormat_ = new Format[] { Format.R16G16_Float, Format.R16G16B16A16_Float };

			shadowMap_ = new Texture(new Texture.InitDesc {
				width = width,
				height = height,
				format = Format.R32_Typeless,
				bindFlag = TextureBuffer.BindFlag.IsDepthStencil,
			});

			blurMap_ = new Texture[2];
			for (int i = 0; i < blurMap_.Length; i++) {
				Texture.InitDesc desc = new Texture.InitDesc() {
					width = width / 2,
					height = height / 2,
					format = bufferFormat_[(int)type_],
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				};
				blurMap_[i] = new Texture(desc);
			}

			frameBuffer_ = new Renderer.FrameBuffer() {
				color_buffer_ = new Texture[0],
				depth_stencil_ = shadowMap_,
			};
			blurFrameBuffer_ = new Renderer.FrameBuffer() {
				color_buffer_ = new Texture[1],
				depth_stencil_ = null,
			};

			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			blurPrim_ = new Prim(shaderName_[(int)type_], (uint)Shader.VertexAttr.TEXCOORD0);
			blurPrim_.AddRect(ref rect);
			blurPrim_.GetMaterial().DepthState = RenderState.DepthState.None;

			camera_ = new Camera();
			camera_.InitializeOrtho(new Vector3(0, 100, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), 100, 100, 1, 100);

			shadowCastShader_ = ShaderManager.FindShader("DrawShadowMap", (uint)(Shader.VertexAttr.POSITION));
			shadowMapDrawShaderFunc_ = (o) => {
				return shadowCastShader_;
			};
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			blurPrim_.Dispose();
			shadowMap_.Dispose();
			foreach (var o in blurMap_) {
				o.Dispose();
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void BeginRender()
		{
			Renderer.CurrentDrawCamera = camera_;
			Renderer.BeginRender(frameBuffer_);
			ShaderManager.UserShaderBindHandler += shadowMapDrawShaderFunc_;
		}


		/// <summary>
		/// 
		/// </summary>
		public void EndRender()
		{
			ShaderManager.UserShaderBindHandler -= shadowMapDrawShaderFunc_;
			Renderer.EndRender();

			// Generate SM
			blurFrameBuffer_.color_buffer_[0] = blurMap_[0];
			Renderer.BeginRender(blurFrameBuffer_);
			blurPrim_.GetMaterial().SetShader(shaderName_[(int)type_]);
			blurPrim_.GetMaterial().SetShaderViewPS(0, shadowMap_);
			blurPrim_.Draw();
			Renderer.EndRender();

			blurFrameBuffer_.color_buffer_[0] = blurMap_[1];
			Renderer.BeginRender(blurFrameBuffer_);
			blurPrim_.GetMaterial().SetShader("GaussianBlurV");
			blurPrim_.GetMaterial().SetShaderViewPS(0, blurMap_[0]);
			blurPrim_.Draw();
			Renderer.EndRender();

			blurFrameBuffer_.color_buffer_[0] = blurMap_[0];
			Renderer.BeginRender(blurFrameBuffer_);
			blurPrim_.GetMaterial().SetShader("GaussianBlurH");
			blurPrim_.GetMaterial().SetShaderViewPS(0, blurMap_[1]);
			blurPrim_.Draw();
			Renderer.EndRender();
		}
	}
}
