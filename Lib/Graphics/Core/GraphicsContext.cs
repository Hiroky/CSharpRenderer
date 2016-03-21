using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;
using SlimDX.Direct3D11;

namespace Lib
{
	/// <summary>
	/// レンダリングコンテキスト
	/// </summary>
	public class GraphicsContext
	{
		private DeviceContext context_;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		public GraphicsContext(DeviceContext context)
		{
			context_ = context;
		}

		/// <summary>
		/// レンダーターゲットの設定
		/// </summary>
		public void SetRenderTargets(ICollection<Texture> colorBuffers, Texture depthStencilBuffer)
		{
			var rtvs = new RenderTargetView[colorBuffers.Count];
			int i = 0;
			foreach(var c in colorBuffers) {
				if (c != null) {
					rtvs[i] = c.RenderTargetView;
				}
				i++;
			}

			var dsv = depthStencilBuffer != null ? depthStencilBuffer.DepthStencilView : null;
			context_.OutputMerger.SetTargets(dsv, rtvs);
		}

		/// <summary>
		/// レンダーターゲットクリア
		/// </summary>
		public void ClearRenderTarget(Texture buffer, Color4 color)
		{
			context_.ClearRenderTargetView(buffer.RenderTargetView, color);
		}

		/// <summary>
		/// デプスバッファクリア
		/// </summary>
		public void ClearDepthStencil(Texture buffer, float value)
		{
			context_.ClearDepthStencilView(buffer.DepthStencilView, DepthStencilClearFlags.Depth, value, 0);
		}

		/// <summary>
		/// ビューポートの設定
		/// </summary>
		public void SetViewport(Viewport viewport)
		{
			context_.Rasterizer.SetViewports(viewport);
		}

		/// <summary>
		/// VS設定
		/// </summary>
		public void SetVsShader(VertexShader vs)
		{
			context_.VertexShader.Set(vs);
		}

		/// <summary>
		/// PS設定
		/// </summary>
		public void SetPsShader(PixelShader ps)
		{
			context_.PixelShader.Set(ps);
		}

		/// <summary>
		/// CS設定
		/// </summary>
		public void SetCsShader(SlimDX.Direct3D11.ComputeShader cs)
		{
			context_.ComputeShader.Set(cs);
		}

		/// <summary>
		/// レイアウト設定
		/// </summary>
		public void SetInputLayout(InputLayout layout)
		{
			context_.InputAssembler.InputLayout = layout;
		}

		/// <summary>
		/// コンスタントバッファ設定
		/// </summary>
		public void SetVsConstantBuffer(SlimDX.Direct3D11.Buffer[] cb, int slot, int count)
		{
			context_.VertexShader.SetConstantBuffers(cb, slot, count);
		}
		public void SetPsConstantBuffer(SlimDX.Direct3D11.Buffer[] cb, int slot, int count)
		{
			context_.PixelShader.SetConstantBuffers(cb, slot, count);
		}
		public void SetCsConstantBuffer(SlimDX.Direct3D11.Buffer[] cb, int slot, int count)
		{
			context_.ComputeShader.SetConstantBuffers(cb, slot, count);
		}

		/// <summary>
		/// シェーダリソースのセット
		/// </summary>
		public void SetShaderResourcePS(int index, IShaderView view)
		{
			context_.PixelShader.SetShaderResource(view.ShaderResourceView, index);
		}
		public void SetShaderResourceVS(int index, IShaderView view)
		{
			context_.VertexShader.SetShaderResource(view.ShaderResourceView, index);
		}
		public void SetShaderResourceCS(int index, IShaderView view)
		{
			context_.ComputeShader.SetShaderResource(view.ShaderResourceView, index);
		}


		/// <summary>
		/// サンプラステートのセット
		/// </summary>
		public void SetSamplerStatePS(int index, SamplerState sampler)
		{
			context_.PixelShader.SetSampler(sampler, index);
		}
		public void SetSamplerStateVS(int index, SamplerState sampler)
		{
			context_.VertexShader.SetSampler(sampler, index);
		}
		public void SetSamplerStateCS(int index, SamplerState sampler)
		{
			context_.ComputeShader.SetSampler(sampler, index);
		}

		/// <summary>
		/// ブレンドステート設定
		/// </summary>
		public void SetBlendState(BlendState state)
		{
			context_.OutputMerger.BlendState = state;
		}

		/// <summary>
		/// デプスステート設定
		/// </summary>
		public void SetDepthState(DepthStencilState state)
		{
			context_.OutputMerger.DepthStencilState = state;
		}

		/// <summary>
		/// ラスタライザステート設定
		/// </summary>
		public void SetRasterizerState(RasterizerState state)
		{
			context_.Rasterizer.State = state;
		}

		/// <summary>
		/// ラスタライザステート設定
		/// </summary>
		public void SetPrimitiveTopology(PrimitiveTopology state)
		{
			context_.InputAssembler.PrimitiveTopology = state;
		}


		public void BeginQuery(Query query)
		{
			context_.Begin(query);
		}

		public void EndQuery(Query query)
		{
			context_.End(query);
		}

		public Type GetQueryData<Type>(Query query)
			where Type : struct
		{
			return context_.GetData<Type>(query);
		}
	}
}
