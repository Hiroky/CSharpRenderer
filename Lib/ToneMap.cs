using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using SlimDX.D3DCompiler;

namespace Lib
{
	public class ToneMap : IDisposable
	{
		Texture luminanceAvg_;
		Texture[] avgBuffer_;
		Prim prim_;
		Renderer.FrameBuffer frameBuffer_;

		Shader downSample4Lum_;
		Shader downSample4_;
		Shader downSample2_;
		Shader toneMap_;
		Shader resolveGamma_;
		ComputeShader lumResolveShader_;
		bool initBuffer_;

		public Texture LuminanceAvgTexture { get { return luminanceAvg_; } }
		public Texture[] AvgTempBuffer { get { return avgBuffer_; } }
		public float KeyValue{ get; set; }

		/// <summary>
		/// 
		/// </summary>
		public ToneMap()
		{
			// サイズによってテクスチャを作り替える、ということをしたくないので
			// FullHD想定でテクスチャを作成しておく
			int w = 1920;
			int h = 1080;
			avgBuffer_ = new Texture[6];
			var desc = new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				width = w / 4,	
				height = h / 4,
				format = SlimDX.DXGI.Format.R16_Float,
			};
			avgBuffer_[0] = new Texture(desc);
			desc.width = w / 16;
			desc.height = h / 16;
			avgBuffer_[1] = new Texture(desc);
			desc.width = w / 32;
			desc.height = h / 32;
			avgBuffer_[2] = new Texture(desc);
			desc.width = w / 64;
			desc.height = h / 64;
			avgBuffer_[3] = new Texture(desc);
			desc.width = w / 128;
			desc.height = h / 128;
			avgBuffer_[4] = new Texture(desc);
			desc.width = w / 256;
			desc.height = h / 256;
			avgBuffer_[5] = new Texture(desc);
			frameBuffer_ = new Renderer.FrameBuffer() {
				color_buffer_ = new Texture[1],
				depth_stencil_ = null,
			};

			var uavDesc_ = new Texture.InitDesc {
				bindFlag = TextureBuffer.BindFlag.IsUnorderedAccess | TextureBuffer.BindFlag.IsRenderTarget,
				width = 1,
				height = 1,
				format = SlimDX.DXGI.Format.R32_Float,	// RWとして使うためには32bitにする必要がある
			};
			luminanceAvg_ = new Texture(uavDesc_);

			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			prim_ = new Prim("Downsample4x4CalcLuminance", (uint)Shader.VertexAttr.TEXCOORD0);
			prim_.AddRect(ref rect);
			prim_.GetMaterial().DepthState = RenderState.DepthState.None;

			downSample4Lum_ = ShaderManager.FindShader("Downsample4x4CalcLuminance", (uint)Shader.VertexAttr.TEXCOORD0);
			downSample4_ = ShaderManager.FindShader("Downsample4x4Luminance", (uint)Shader.VertexAttr.TEXCOORD0);
			downSample2_ = ShaderManager.FindShader("Downsample2x2Luminance", (uint)Shader.VertexAttr.TEXCOORD0);
			toneMap_ = ShaderManager.FindShader("ToneMap", (uint)Shader.VertexAttr.TEXCOORD0);
			resolveGamma_ = ShaderManager.FindShader("ResolveGamma", (uint)Shader.VertexAttr.TEXCOORD0);

			ComputeShader.InitDesc shaderDesc = new ComputeShader.InitDesc {
				file_name = "asset/shader/ToneMap.fx",
				id = 0,
				is_byte_code = false,
				main = "FinalCalculateAverageLuminance",
			};
			lumResolveShader_ = new ComputeShader(shaderDesc);
			lumResolveShader_.SetUAVs(new Texture[] { luminanceAvg_ });
			lumResolveShader_.SetResources(new Texture[] { avgBuffer_[5] });
			initBuffer_ = false;

			KeyValue = 0.2f;
			Shader.SetConstantBufferUpdateFunc("CB_ToneMap", (s, i) => {
				dynamic cb = i;
				if (cb.g_keyValue != KeyValue) {
					cb.g_keyValue = KeyValue;
					return true;
				}
				return false;
			});
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			foreach (var t in avgBuffer_) {
				t.Dispose();
			}
			luminanceAvg_.Dispose();
			prim_.Dispose();
			lumResolveShader_.Dispose();
		}


		/// <summary>
		/// 輝度算出
		/// </summary>
		public void CalcLuminance(Texture colorTex)
		{
			// 1/4
			frameBuffer_.color_buffer_[0] = avgBuffer_[0];
			Renderer.BeginRender(frameBuffer_);
			prim_.GetMaterial().SetShader(downSample4Lum_);
			prim_.GetMaterial().SetShaderViewPS(0, colorTex);
			prim_.Draw();
			Renderer.EndRender();

			// 1/4
			frameBuffer_.color_buffer_[0] = avgBuffer_[1];
			Renderer.BeginRender(frameBuffer_);
			prim_.GetMaterial().SetShader(downSample4_);
			prim_.GetMaterial().SetShaderViewPS(0, avgBuffer_[0]);
			prim_.Draw();
			Renderer.EndRender();

			// 1/2
			frameBuffer_.color_buffer_[0] = avgBuffer_[2];
			Renderer.BeginRender(frameBuffer_);
			prim_.GetMaterial().SetShader(downSample2_);
			prim_.GetMaterial().SetShaderViewPS(0, avgBuffer_[1]);
			prim_.Draw();
			Renderer.EndRender();

			// 1/2
			frameBuffer_.color_buffer_[0] = avgBuffer_[3];
			Renderer.BeginRender(frameBuffer_);
			prim_.GetMaterial().SetShaderViewPS(0, avgBuffer_[2]);
			prim_.Draw();
			Renderer.EndRender();

			// 1/2
			frameBuffer_.color_buffer_[0] = avgBuffer_[4];
			Renderer.BeginRender(frameBuffer_);
			prim_.GetMaterial().SetShaderViewPS(0, avgBuffer_[3]);
			prim_.Draw();
			Renderer.EndRender();

			// 1/2
			frameBuffer_.color_buffer_[0] = avgBuffer_[5];
			Renderer.BeginRender(frameBuffer_);
			prim_.GetMaterial().SetShaderViewPS(0, avgBuffer_[4]);
			prim_.Draw();
			Renderer.EndRender();

			// 最終
			if (!initBuffer_) {
				Renderer.ClearColorBuffer(luminanceAvg_, new Color4(0, 0, 0, 0));
				initBuffer_ = true;
			} 
			lumResolveShader_.Bind();
			lumResolveShader_.Dispatch(1, 1, 1);
			lumResolveShader_.UnBind();
		}



		/// <summary>
		/// HDR解決
		/// </summary>
		/// <param name="uav"></param>
		public void ResolveHDR(Texture colorTex)
		{
			prim_.GetMaterial().SetShader(toneMap_);
			prim_.GetMaterial().SetShaderViewPS(0, colorTex);
			prim_.GetMaterial().SetShaderViewPS(1, luminanceAvg_);
			prim_.Draw();
		}

		/// <summary>
		/// ガンマの解決のみ実行
		/// </summary>
		/// <param name="colorTex"></param>
		public void ResolveGamma(Texture colorTex)
		{
			prim_.GetMaterial().SetShader(resolveGamma_);
			prim_.GetMaterial().SetShaderViewPS(0, colorTex);
			prim_.Draw();
		}
	}
}
