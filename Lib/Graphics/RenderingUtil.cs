using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;

namespace Lib
{
	static public class RenderingUtil
	{
		static Prim prim_;

		static public void Initialize()
		{
			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			prim_ = new Prim("Direct", (uint)Shader.VertexAttr.TEXCOORD0);
			prim_.AddRect(ref rect);
			prim_.GetMaterial().DepthState = RenderState.DepthState.None;
		}

		static public void Dispose()
		{
			prim_.Dispose();
		}

		
		static public void DrawScreen(GraphicsContext context, Shader shader, Texture[] textures)
		{
			for(int i = 0; i < textures.Length; i++){
				prim_.GetMaterial().SetShaderViewPS(i, textures[i]);
			}
			prim_.GetMaterial().SetShader(shader);
			prim_.Draw(context);
		}
	}
}
