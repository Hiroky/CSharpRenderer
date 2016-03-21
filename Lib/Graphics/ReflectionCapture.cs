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
	/// 反射キャプチャ
	/// </summary>
	public class ReflectionCapture : IDisposable
	{
		static Dictionary<int, Texture> depthBuffers_;
		static Model debugModel_;
		static Vector4[] currentSHCoef_;
		static int refCount_ = 0;

		Texture cubeMap_;
		Texture radianceMap_;
		Texture depth_;
		
		public Vector3 Position { get; set; }
		public Vector4[] SHCoef { get; set; }
		public Texture Map { get { return cubeMap_; } }
		public Texture RadianceMap { get { return radianceMap_; } }


		/// <summary>
		/// 
		/// </summary>
		/// <param name="size"></param>
		public ReflectionCapture(int size)
		{
			Initialize(size, Vector3.Zero, false);
		}


		/// <summary>
		/// 
		/// </summary>
		public ReflectionCapture(int size, Vector3 position, bool calcRadiance = false)
		{
			Initialize(size, position, calcRadiance);
		}


		/// <summary>
		/// 
		/// </summary>
		void Initialize(int size, Vector3 pos, bool calcRadiance)
		{
			cubeMap_ = new Texture(new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				optionFlags = ResourceOptionFlags.TextureCube,
				width = size,
				height = size,
				//format = SlimDX.DXGI.Format.R16G16B16A16_Float,	// 本来はこっちが正しい
				format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,			// 仮
			});

			if (depthBuffers_ == null || !depthBuffers_.ContainsKey(size)) {
				if (depthBuffers_ == null) {
					depthBuffers_ = new Dictionary<int, Texture>();
				}
				var depth = new Texture(new Texture.InitDesc {
					width = size,
					height = size,
					format = SlimDX.DXGI.Format.R32_Typeless,
					bindFlag = TextureBuffer.BindFlag.IsDepthStencil,
				});
				depthBuffers_[size] = depth;
			}
			depth_ = depthBuffers_[size];

			if (calcRadiance) {
				radianceMap_ = new Texture();
			}


#if false
			if (debugModel_ == null) {
				debugModel_ = new Model();
				debugModel_.Initialize("asset/sphere.dae");
				//debugModel_.Materials[0].SetShader("Skybox", (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL));
				debugModel_.Materials[0].SetShader("DebugSH", (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL));

				// デバッグ表示用
				Shader.SetConstantBufferUpdateFunc("CB_SH", (s, i) => {
					if (currentSHCoef_ != null) {
						dynamic cb = i;
						cb.g_shCoef_0 = currentSHCoef_[0];
						cb.g_shCoef_1 = currentSHCoef_[1];
						cb.g_shCoef_2 = currentSHCoef_[2];
						cb.g_shCoef_3 = currentSHCoef_[3];
						return true;
					}
					return false;
				});
			}
#endif

			Position = pos;
			refCount_++;
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			cubeMap_.Dispose();
			refCount_--;
			if (refCount_ == 0) {
				if (depthBuffers_ != null) {
					foreach (var b in depthBuffers_) {
						b.Value.Dispose();
					}
					depthBuffers_ = null;
				}
				if (debugModel_ != null) {
					debugModel_.Dispose();
					debugModel_ = null;
				}
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void Capture(GraphicsContext context, Action renderFunc, uint shBand = 0)
		{
			// レンダリング
			CubemapHelper.RenderingCubeMap(context, cubeMap_, depth_, Position, renderFunc);

			// PMREM
			if (radianceMap_ != null) {
				CubemapHelper.PrefilterRadiance(radianceMap_, cubeMap_);
			}

			// SH計算
			if (shBand > 0) {
				SHCoef = SHHelper.GetSphericalHarmonics(cubeMap_, shBand);
			}
		}


		/// <summary>
		/// デバッグ描画
		/// </summary>
		public void DebugDraw()
		{
			if (debugModel_ != null) {
				currentSHCoef_ = SHCoef;
				debugModel_.Materials[0].SetShaderViewPS(0, Map);
				debugModel_.WorldMatrix = Matrix.Translation(Position);
				debugModel_.Draw();
			}
		}
	}
}
