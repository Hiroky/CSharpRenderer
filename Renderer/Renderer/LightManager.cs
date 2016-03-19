using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lib;
using SlimDX;

namespace Renderer
{
	/// <summary>
	/// ライト管理クラス
	/// </summary>
	class LightManager : IDisposable
	{
		public class Light
		{
			public Vector3 pos;
			public float radius;
			public Vector3 color;
			public float intensity;
		}
		const int structSize = 32;

		Light[] lightList_;
		DataStream stream_;
		StructuredBuffer buffer_;
		bool updated_;
		int count_;

		Prim debugPrim_;

		public IShaderView LightBuffer { get { return buffer_; } }
		public int LightCount { get { return count_; } set { count_ = value; } }
		public Vector3 DirectionalLightDir { get; set; }
		public float DirectionalLightIntensity { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="max_num"></param>
		public LightManager(int max_num)
		{
			int size = structSize * max_num;
			count_ = max_num;

			lightList_ = new Light[max_num];
			stream_ = new DataStream(size, true, true);

			// 構造化バッファ生成
			StructuredBuffer.BufferDesc desc = new StructuredBuffer.BufferDesc {
				bindUAV = true,
				stride = structSize,
				size = (int)size,
				initData = null,
			};
			buffer_ = new StructuredBuffer(desc);

#if true
			// ライトセットアップ(仮)
			Random rand = new Random(0);
			Vector3[] colorAry = new Vector3[] {
				new Vector3(1, 0, 0),
				new Vector3(0, 1, 0),
				new Vector3(0, 0, 1),
				new Vector3(1, 1, 1),
			};
			float scale = 0.2f;
			float range_x = 150 * scale;
			float range_y = 70 * scale;
			float range_z = 65 * scale;
			for (int i = 0; i < count_; i++) {
				var light = new Light();
				float x = (float)(rand.NextDouble() - 0.5) * 2 * range_x;
				float y = (float)(rand.NextDouble() - 0.0) * 2 * range_y;
				float z = (float)(rand.NextDouble() - 0.5) * 2 * range_z;
				light.pos = new Vector3(x, y, z);
				light.color = colorAry[i % colorAry.Length];
				light.radius = 5;
				light.intensity = 4.0f;
				SetLight(i, light);
			}
#else
			var light = new Light();
			light.pos = new Vector3(0,10,0);
			light.color = new Vector3(1, 1, 1);
			light.intensity = 80;

			SetLight(0, light);
#endif

			DirectionalLightDir = new Vector3(0.3f, -1.0f, 0.2f);
			DirectionalLightDir.Normalize();
			DirectionalLightIntensity = 2;

			//float primSize = 2.0f;
			float primSize = 0.5f;
			Lib.Rect rect = new Lib.Rect(new Vector3(-primSize, primSize, 0.0f), new Vector3(primSize, -primSize, 0.0f));
			debugPrim_ = new Prim("DebugDrawPointLights", (uint)Shader.VertexAttr.TEXCOORD0);
			debugPrim_.AddRect(ref rect);
			debugPrim_.GetMaterial().SetShaderViewVS(0, LightBuffer);
			debugPrim_.GetMaterial().SetShaderViewPS(0, LightBuffer);
			debugPrim_.GetMaterial().BlendState = RenderState.BlendState.Add;
			debugPrim_.GetMaterial().DepthState = RenderState.DepthState.TestOnly;
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			stream_.Close();
			buffer_.Dispose();
			debugPrim_.Dispose();
		}


		/// <summary>
		/// バッファの更新などを行う
		/// </summary>
		public void Update()
		{
			if (updated_) {
				stream_.Position = 0;
				buffer_.SetBufferData(stream_);
				updated_ = false;
			}
		}


		/// <summary>
		/// 指定インデックスのライトをセット
		/// </summary>
		public void SetLight(int index, Light l)
		{
			lightList_[index] = l;

			// ストリームに書き込み
			int offset = structSize * index;
			stream_.Position = offset;
			stream_.Write(l.pos);
			stream_.Write(l.radius);
			stream_.Write(l.color);
			stream_.Write(l.intensity);

			updated_ = true;
		}


		public void DebugDraw()
		{
			debugPrim_.Draw((int)count_);
		}
	}
}
