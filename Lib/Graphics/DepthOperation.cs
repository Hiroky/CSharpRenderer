using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib
{
	public class DepthOperation : IDisposable
	{
		Texture linearBuffer_;
		Texture linearHalfBuffer_;
		Shader linearizeShader_;
		Shader halfLinearizeShader_;
		GraphicsCore.FrameBuffer frameBuffer_;

		struct DepthParam
		{
			public float g_ReprojectDepthScale;
			public float g_ReprojectDepthBias;
		};
		static DepthParam param_ = new DepthParam();

		public Texture LinearDepthBuffer { get { return linearBuffer_; } }

		/// <summary>
		/// 
		/// </summary>
		public DepthOperation(int w, int h)
		{
			ConstructTexture(w, h);

			linearizeShader_ = ShaderManager.FindShader("LinearizeDepth", (uint)Shader.VertexAttr.TEXCOORD0);
			halfLinearizeShader_ = ShaderManager.FindShader("Downsample2x2LinealizeDepth", (uint)Shader.VertexAttr.TEXCOORD0);
			Shader.SetConstantBufferUpdateFunc("DepthParam", (s, i) => {
				dynamic cb = i;
				cb.g_ReprojectDepthScale = param_.g_ReprojectDepthScale;
				cb.g_ReprojectDepthBias = param_.g_ReprojectDepthBias;
				return true;
			});

			frameBuffer_ = new GraphicsCore.FrameBuffer();
		}

		/// <summary>
		/// 
		/// </summary>
		void ConstructTexture(int w, int h)
		{
			var desc = new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				width = w,
				height = h,
				format = SlimDX.DXGI.Format.R16_Float,
			};
			if( linearBuffer_ == null ) {
				linearBuffer_ = new Texture();
				linearHalfBuffer_ = new Texture();
			} else {
				linearBuffer_.Dispose();
				linearHalfBuffer_.Dispose();
			}
			linearBuffer_.Initialize(desc);
			desc.width /= 2;
			desc.height /= 2;
			linearHalfBuffer_.Initialize(desc);
		}

		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			linearHalfBuffer_.Dispose();
			linearBuffer_.Dispose();
		}

		/// <summary>
		/// 
		/// </summary>
		public void SizeChanged(int w, int h)
		{
			ConstructTexture(w, h);
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispatch(GraphicsContext context, Texture depth, Camera camera)
		{
			float near = camera.Near;
			float far = camera.Far;
			param_.g_ReprojectDepthScale = (far - near) / (-far * near);
			param_.g_ReprojectDepthBias = far / (far * near);
			frameBuffer_.color_buffer_ = new Texture[1] { linearBuffer_ };
			frameBuffer_.depth_stencil_ = null;
			context.SetRenderTargets(frameBuffer_.color_buffer_, frameBuffer_.depth_stencil_);
			RenderingUtil.DrawScreen(context, linearizeShader_, new Texture[] { depth });
		}
	}
}
