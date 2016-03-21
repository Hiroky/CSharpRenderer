using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Lib;
using ksRenderer = Lib.GraphicsCore;
using SlimDX;
using SlimDX.D3DCompiler;

namespace Renderer
{
	class ComputeManager : IDisposable
	{
		public enum Method
		{
			Result,
			NoUseTiled,
			Debug_LightCount,

			Max,
		}

		const uint TILE_SIZE = 16;

		ComputeShader[] shaders_;
		IShaderView[] resources_;
		uint width_, height_;

		public int Index { get { return (int)CurrentMethod; } }
		public Method CurrentMethod { get; set; }
		public ShadowMap ShadowMap { get; set; }
		public LightManager LightManager { get; set; }
		public CubeMapGenInfo CubeMapInfo { get; set; }

		/// <summary>
		/// 
		/// </summary>
		public ComputeManager()
		{
			shaders_ = new ComputeShader[(int)Method.Max];
			ComputeShader.InitDesc desc = new ComputeShader.InitDesc {
				file_name = "asset/shader/LightAccumulate.fx",
				id = 0,
				is_byte_code = false,
				main = "main",
			};
			desc.macro = new ShaderMacro[] {
				new ShaderMacro("NO_CALC_POINT_LIGHT"),
				//new ShaderMacro("NO_CALC_DIRECTIONAL_LIGHT"),
			};
			shaders_[0] = new ComputeShader(desc);

			desc.macro = new ShaderMacro[] {
				new ShaderMacro("NO_USE_TILED"),
			};
			shaders_[1] = new ComputeShader(desc);

			desc.macro = new ShaderMacro[] {
				new ShaderMacro("DEBUG_LIGHT_COUNT"),
			};
			shaders_[2] = new ComputeShader(desc);

			CurrentMethod = Method.Result;
			resources_ = new IShaderView[9];
			foreach (var s in shaders_) {
				s.SetResources(resources_);
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			foreach (var s in shaders_) {
				s.Dispose();
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void ScreenSizeChanged(int w, int h)
		{
			width_ = (uint)w;
			height_ = (uint)h;
		}


		/// <summary>
		/// 
		/// </summary>
		public void SetResources(IShaderView[] resources)
		{
			int i = 0;
			foreach (var s in resources) {
				resources_[i++] = s;
			}
		}
		public void SetResources(int index, IShaderView resource)
		{
			resources_[index] = resource;
		}


		/// <summary>
		/// 
		/// </summary>
		public void SetUAVs(IShaderView[] uavs)
		{
			foreach (var s in shaders_) {
				s.SetUAVs(uavs);
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void Bind()
		{
			dynamic cb = shaders_[Index].CBInstance;
			Camera camera = ksRenderer.Camera3D;

			Matrix invProj = Matrix.Invert(camera.ProjectionMatrix);
			invProj = Matrix.Transpose(invProj);
			cb.g_invProjMatrix = invProj;

			Matrix invViewProj = Matrix.Multiply(camera.ViewMatrix, camera.ProjectionMatrix);
			invViewProj.Invert();
			cb.g_invViewProjMatrix = Matrix.Transpose(invViewProj);

			Matrix viewMat = Matrix.Transpose(camera.ViewMatrix);
			cb.g_viewMatrix = viewMat;
			cb.g_viewPos = camera.Position;

			cb.g_uNumTilesX = (uint)((width_ + TILE_SIZE - 1) / (float)TILE_SIZE);
			cb.g_uNumTilesY = (uint)((height_ + TILE_SIZE - 1) / (float)TILE_SIZE);
			cb.g_pointLightCount = (uint)LightManager.LightCount;

			cb.g_directionalLightDir = LightManager.DirectionalLightDir;
			cb.g_directionalLightIntensity = LightManager.DirectionalLightIntensity;
			cb.g_shadowMatrix = Matrix.Transpose(ShadowMap.Camera.ViewProjectionMatrix);

			cb.g_boundingBoxMin = CubeMapInfo.boundingMin;
			cb.g_boundingBoxMax = CubeMapInfo.boundingMax;

			shaders_[Index].UpdateConstantBuffer();
			shaders_[Index].Bind();
			ksRenderer.SetSamplerStateCS(0, RenderState.TextureAddressing.Border0, RenderState.TextureAddressing.Border0);
			ksRenderer.SetSamplerStateCS(1, RenderState.TextureAddressing.Clamp, RenderState.TextureAddressing.Clamp);
		}


		/// <summary>
		/// 
		/// </summary>
		public void UnBind()
		{
			shaders_[Index].UnBind();
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispatch(int w, int h)
		{
			int x = Lib.ComputeShader.CalcThreadGroups(w, (int)TILE_SIZE);
			int y = Lib.ComputeShader.CalcThreadGroups(h, (int)TILE_SIZE);
			shaders_[Index].Dispatch(x, y, 1);
		}
	}
}
