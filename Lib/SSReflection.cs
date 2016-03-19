using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib
{
	class SSReflection : IDisposable
	{
		Texture rayTraceBuffer_;
		Shader rayTraceShader_;

		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		public SSReflection(int width, int height)
		{
			AllocateTexture(width, height);

			rayTraceShader_ = ShaderManager.FindShader("SSRayTrace", (uint)Shader.VertexAttr.TEXCOORD0);
			//Shader.SetConstantBufferUpdateFunc("SSRParam", (s, i) => {
			//	dynamic cb = i;
			//	if (cb.g_keyValue != KeyValue) {
			//		cb.g_keyValue = KeyValue;
			//		return true;
			//	}
			//	return false;
			//});
		}

		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			rayTraceBuffer_.Dispose();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		public void ScreenSizeChanged(int width, int height)
		{
			AllocateTexture(width, height);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="w"></param>
		/// <param name="h"></param>
		void AllocateTexture(int w, int h)
		{
			if (rayTraceBuffer_ != null) {
				rayTraceBuffer_.Dispose();
			} else {
				rayTraceBuffer_ = new Texture();
			}

			var desc = new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				width = w / 2,
				height = h / 2,
				format = SlimDX.DXGI.Format.R16G16B16A16_Float,
			};
			rayTraceBuffer_.Initialize(desc);
		}
	}
}
