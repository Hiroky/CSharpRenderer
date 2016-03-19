using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lib;
using SlimDX;
using SlimDX.Direct3D11;
using DXMatrix = SlimDX.Matrix;
using ksRenderer = Lib.Renderer;
using ksGpuProfilePoint = Lib.Ext.GpuProfilePoint;

namespace Renderer
{
	class TestScene : IScene
	{
		Vector3 camera_at_ = new Vector3(0, 0, 0);
		CameraController cameraCtrl_;

		Texture[] gbuffers_;
		Texture.InitDesc[] gbufferDesc_;
		Texture.InitDesc hdrResolveDesc_;
		Texture hdrResolveBuffer_;
		Texture lineTexture_;
		ksRenderer.FrameBuffer gBufferBinder_;
		ksRenderer.FrameBuffer hdrResultBuffer_;

		Prim prim_;
		Prim fxaaPrim_;

		Model drawModel_;

		Texture[] debugViewList_;

		Material lineSpriteMaterial_ = null;

		MainWindowViewModel viewModel_;
		public MainWindowViewModel ViewModel { get { return viewModel_; } }


		/// <summary>
		/// 
		/// </summary>
		public TestScene()
		{
			viewModel_ = new MainWindowViewModel();

			// カメラ操作
			ksRenderer.Camera3D.SetAt(ref camera_at_);
			cameraCtrl_ = new CameraController(ksRenderer.Camera3D);

			InitializeTarget();
			InitialzeSceneObject();
		}

		/// <summary>
		/// レンダーターゲット初期化
		/// </summary>
		void InitializeTarget()
		{
			// deferred用MRTバッファ
			gbufferDesc_ = new Texture.InitDesc[] {
				new Texture.InitDesc() {
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
					width = ksRenderer.TargetWidth,
					height = ksRenderer.TargetHeight,
					format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
				},
				new Texture.InitDesc() {
					bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
					width = ksRenderer.TargetWidth,
					height = ksRenderer.TargetHeight,
					format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
				},
			};
			gbuffers_ = new Texture[gbufferDesc_.Length];
			for (int i = 0; i < gbuffers_.Length; i++) {
				gbuffers_[i] = new Texture(gbufferDesc_[i]);
			}
			gBufferBinder_ = new ksRenderer.FrameBuffer();
			gBufferBinder_.color_buffer_ = gbuffers_;
			gBufferBinder_.depth_stencil_ = ksRenderer.DefaultDepthBuffer;

			hdrResolveDesc_ = new Texture.InitDesc() {
				bindFlag = TextureBuffer.BindFlag.IsRenderTarget,
				width = ksRenderer.TargetWidth,
				height = ksRenderer.TargetHeight,
				format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
			};
			hdrResolveBuffer_ = new Texture(hdrResolveDesc_);

			hdrResultBuffer_ = new ksRenderer.FrameBuffer();
			hdrResultBuffer_.color_buffer_ = new Texture[] { hdrResolveBuffer_ };
			hdrResultBuffer_.depth_stencil_ = null;

			// アンチなし用プリム
			Lib.Rect rect = new Lib.Rect(new Vector3(-1.0f, 1.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));
			prim_ = new Prim("Outline", (uint)Shader.VertexAttr.TEXCOORD0);
			prim_.AddRect(ref rect);
			prim_.GetMaterial().SetShaderViewPS(0, gbuffers_[0]);
			prim_.GetMaterial().DepthState = RenderState.DepthState.None;

			// FXAA	
			fxaaPrim_ = new Prim("FXAA", (uint)Shader.VertexAttr.TEXCOORD0);
			fxaaPrim_.AddRect(ref rect);
			fxaaPrim_.GetMaterial().SetShaderViewPS(0, hdrResolveBuffer_);
			fxaaPrim_.GetMaterial().DepthState = RenderState.DepthState.None;

			// ラインスプライト
			lineSpriteMaterial_ = new Material();
			lineSpriteMaterial_.SetShader("LineSprite", 0);
			lineTexture_ = new Texture("asset/texture/circle32x32_2.png");

			Lib.Shader.SetConstantBufferUpdateFunc("CB_LineSprite", (o, e) => {
				dynamic cb = e;
				cb.g_screenScale = new Vector4(hdrResolveBuffer_.Width, hdrResolveBuffer_.Height, 1.0f / hdrResolveBuffer_.Width, 1.0f / hdrResolveBuffer_.Height);
				return true;
			});

			// デバッグ表示用
			debugViewList_ = new Texture[] {
				hdrResolveBuffer_,
				gbuffers_[0],
				gbuffers_[1],
			};
		}


		/// <summary>
		/// オブジェクト初期化
		/// </summary>
		void InitialzeSceneObject()
		{
			drawModel_ = new Model();
			drawModel_.Initialize("D:/Development/Model/Convert/dae/azusa.dae");
			foreach (var m in drawModel_.Materials) {
				m.SetShader("Cell", (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0));
			}
		}


		/// <summary>
		/// 
		/// </summary>
		public void Dispose()
		{
			prim_.Dispose();
			if (drawModel_ != null) drawModel_.Dispose();
			fxaaPrim_.Dispose();
			foreach (var t in gbuffers_) {
				t.Dispose();
			}
			hdrResolveBuffer_.Dispose();
			lineTexture_.Dispose();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="w"></param>
		/// <param name="h"></param>
		public void ScreenSizeChanged(int w, int h)
		{
			// GBuffer
			for (int i = 0; i < gbufferDesc_.Length; i++) {
				gbufferDesc_[i].width = w;
				gbufferDesc_[i].height = h;
				gbuffers_[i].Dispose();
				gbuffers_[i].Initialize(gbufferDesc_[i]);
			}
			gBufferBinder_.depth_stencil_ = ksRenderer.DefaultDepthBuffer;
			hdrResolveBuffer_.Dispose();
			hdrResolveDesc_.width = w;
			hdrResolveDesc_.height = h;
			hdrResolveBuffer_.Initialize(hdrResolveDesc_);

			// カラーバッファ作り替え
			fxaaPrim_.GetMaterial().SetShaderViewPS(0, hdrResolveBuffer_);
		}


		/// <summary>
		/// 更新
		/// </summary>
		public void Update()
		{
			// 入力
			Input.Update();

			// カメラ更新
			cameraCtrl_.Update();
		}


		/// <summary>
		/// 描画関連の更新
		/// </summary>
		public void UpdateDraw()
		{
			// レンダラ更新
			ksRenderer.Update();
		}


		bool test = true;

		/// <summary>
		/// 描画
		/// </summary>
		public void Draw()
		{
			UpdateDraw();

			ksRenderer.BeginDraw();
			{
				// GBuffer
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Render GBuffer")) {
					ksRenderer.CurrentDrawCamera = ksRenderer.Camera3D;
					RenderGBuffer();
				}

				var framebuffer = new ksRenderer.FrameBuffer();
				var edgeBuffer = hdrResolveBuffer_;
				// アウトライン検出
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "Outline")) {
					framebuffer.color_buffer_ = new Texture[] { hdrResolveBuffer_ };
					ksRenderer.BeginRender(framebuffer);
					prim_.GetMaterial().SetShader("Outline");
					prim_.GetMaterial().SetShaderViewPS(0, gbuffers_[0]);
					prim_.GetMaterial().SetShaderViewPS(1, gbuffers_[1]);
					prim_.GetMaterial().BlendState = RenderState.BlendState.None;
					prim_.Draw();
					ksRenderer.EndRender();

					//framebuffer.color_buffer_ = new Texture[] { gbuffers_[1] };
					//ksRenderer.BeginRender(framebuffer);
					//fxaaPrim_.GetMaterial().SetShaderViewPS(0, hdrResolveBuffer_);
					//fxaaPrim_.Draw();
					//ksRenderer.EndRender();
					//edgeBuffer = gbuffers_[1];
				}

				// vertexIDによるスプライト描画
				using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "LineSprite")) {
					framebuffer.color_buffer_ = new Texture[] { gbuffers_[0] };
					ksRenderer.BeginRender(framebuffer);
					if (test) {
						ksRenderer.D3dCurrentContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding());
						lineSpriteMaterial_.SetShaderViewVS(0, edgeBuffer);
						lineSpriteMaterial_.SetShaderViewPS(0, lineTexture_);
						lineSpriteMaterial_.DepthState = RenderState.DepthState.None;
						lineSpriteMaterial_.BlendState = RenderState.BlendState.Normal;
						lineSpriteMaterial_.Setup();
						Matrix ident = Matrix.Identity;
						ShaderManager.SetUniformParams(ref ident);
						ksRenderer.SetRasterizerState(RenderState.RasterizerState.CullNone);
						int instance = hdrResolveBuffer_.Width * hdrResolveBuffer_.Height;
						ksRenderer.D3dCurrentContext.DrawInstanced(6, instance, 0, 0);
					} else {
						prim_.GetMaterial().SetShader("Direct");
						prim_.GetMaterial().SetShaderViewPS(0, hdrResolveBuffer_);
						prim_.GetMaterial().BlendState = RenderState.BlendState.Normal;
						prim_.Draw();
					}
					ksRenderer.EndRender();
				}


				// 最終レンダリング
				ksRenderer.BeginRender();
				{
					// ライティング結果
					using (new ksGpuProfilePoint(ksRenderer.D3dCurrentContext, "FXAA")) {
						fxaaPrim_.GetMaterial().SetShaderViewPS(0, gbuffers_[0]);
						fxaaPrim_.GetMaterial().BlendState = RenderState.BlendState.None;
						fxaaPrim_.Draw();
					}
				}
				ksRenderer.EndRender();
			}
			ksRenderer.EndDraw();
		}


		/// <summary>
		/// シーン描画
		/// </summary>
		void SceneDraw()
		{
			if (drawModel_ != null) {
				drawModel_.Draw();
			}
		}


		/// <summary>
		/// GBufferへの描画
		/// </summary>
		void RenderGBuffer()
		{
			ksRenderer.BeginRender(gBufferBinder_);

			ksRenderer.ClearColorBuffer(gBufferBinder_.color_buffer_[0], new Color4(0.5f, 0.5f, 0.5f));
			ksRenderer.ClearColorBuffer(gBufferBinder_.color_buffer_[1], new Color4(0));
			ksRenderer.ClearDepthBuffer();

			SceneDraw();

			ksRenderer.EndRender();
		}


		/// <summary>
		/// モデルのロード
		/// </summary>
		/// <param name="name"></param>
		public void LoadModel(string name)
		{
			if (drawModel_ != null) {
				drawModel_.Dispose();
			}
			drawModel_.Initialize(name, 1, true);
			foreach (var m in drawModel_.Materials) {
				int count = m.PsShaderViews.Count(c => c != null);
				if (count > 1) {
					// 法線マップ付
					m.SetShader("GBufferAccumulate");
				} else {
					m.SetShader("GBufferAccumulate", (uint)(Shader.VertexAttr.POSITION | Shader.VertexAttr.NORMAL | Shader.VertexAttr.TEXCOORD0));
				}
			}
		}
	}
}
