using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;

namespace Lib
{
	public class Camera
	{
		Vector3 position_;
		Vector3 at_;
		Vector3 up_;
		float fovy_;
		float aspect_;
		float near_;
		float far_;
		float width_;
		float height_;
		bool isUpdated_;
		bool isNeedUpdate_;

		Matrix viewMatrix_;
		Matrix projectionMatrix_;
		Matrix viewProjectionMatrix_;

		public Vector3 Position { get { return position_; } }
		public Vector3 At { get { return at_; } }
		public Vector3 Up { get { return up_; } }
		public Matrix ViewMatrix { get { return viewMatrix_; } }
		public Matrix ProjectionMatrix { get { return projectionMatrix_; } }
		public Matrix ViewProjectionMatrix { get { return viewProjectionMatrix_; } }
		public bool IsUpdated { get { return isUpdated_; } }
		public float Near { get { return near_; } }
		public float Far { get { return far_; } }

		public Action UpdateFunc_;

		/// <summary>
		/// 
		/// </summary>
		public Camera()
		{
		}


		/// <summary>
		/// 透視投影で初期化
		/// </summary>
		public void InitializePerspective(Vector3 pos, Vector3 at, Vector3 up, float fovy, float z_near, float z_far, float aspect)
		{
			position_ = pos;
			at_ = at;
			up_ = up;
			fovy_ = fovy;
			near_ = z_near;
			far_ = z_far;
			aspect_ = aspect;
			isNeedUpdate_ = true;

			UpdateFunc_ = () => {
				Matrix.LookAtRH(ref position_, ref at_, ref up_, out viewMatrix_);
				Matrix.PerspectiveFovRH(fovy_, aspect_, near_, far_, out projectionMatrix_);
				Matrix.Multiply(ref viewMatrix_, ref projectionMatrix_, out viewProjectionMatrix_);
			};
			Update();
		}


		public void InitializeOrtho(Vector3 pos, Vector3 at, Vector3 up, float width, float height, float z_near, float z_far)
		{
			position_ = pos;
			at_ = at;
			up_ = up;
			fovy_ = 0;
			near_ = z_near;
			far_ = z_far;
			aspect_ = width / height;
			isNeedUpdate_ = true;

			UpdateFunc_ = () => {
				Matrix.LookAtRH(ref position_, ref at_, ref up_, out viewMatrix_);
				Matrix.OrthoRH(width, height, z_near, z_far, out projectionMatrix_);
				Matrix.Multiply(ref viewMatrix_, ref projectionMatrix_, out viewProjectionMatrix_);
			};
			Update();
		}


		/// <summary>
		/// 外部からマトリクス指定する
		/// </summary>
		public void InitializeExternal(Matrix view, Matrix projection)
		{
			viewMatrix_ = view;
			projectionMatrix_ = projection;

			UpdateFunc_ = () => {
				Matrix.Multiply(ref viewMatrix_, ref projectionMatrix_, out viewProjectionMatrix_);
			};
			Update();
		}

		/// <summary>
		/// 更新処理
		/// </summary>
		public void Update()
		{
			isUpdated_ = false;

			if (isNeedUpdate_) {
				UpdateFunc_();
				isNeedUpdate_ = false;
				isUpdated_ = true;
			}
		}


		public void SetPosition(ref Vector3 pos)
		{
			position_ = pos;
			isNeedUpdate_ = true;
		}

		public void SetAt(ref Vector3 at)
		{
			at_ = at;
			isNeedUpdate_ = true;
		}

		public void SetUp(ref Vector3 up)
		{
			up_ = up;
			isNeedUpdate_ = true;
		}

		public void SetAspect(float aspect)
		{
			aspect_ = aspect;
			isNeedUpdate_ = true;
		}

		public void SetNear(float near)
		{
			near_ = near;
			isNeedUpdate_ = true;
		}

		public void SetFar(float far)
		{
			far_ = far;
			isNeedUpdate_ = true;
		}

		public void SetOrtho(float width, float height)
		{
			width_ = width;
			height_ = height;
			isNeedUpdate_ = true;
		}

		public void SetViewMatrix(ref Matrix mat)
		{
			viewMatrix_ = mat;
			isNeedUpdate_ = true;
		}

		public void SetProjectionMatrix(ref Matrix mat)
		{
			projectionMatrix_ = mat;
			isNeedUpdate_ = true;
		}
	}


	/// <summary>
	/// カメラの基本的な操作クラス
	/// </summary>
	public class CameraController
	{
		Camera camera_;
		Vector3 at_;
		Vector3 rotation_;
		float length_;

		readonly float MIN_ROTATION_X = Lib.Math.DegToRad(-89);
		readonly float MAX_ROTATION_X = Lib.Math.DegToRad(89);
		const float ROTATE_UNIT = 0.5f;
		const float ZOOM_UNIT = 0.1f;
		const float TRANCE_UNIT = 0.05f;

		public CameraController(Camera camera)
		{
			camera_ = camera;
			Vector3 dir = camera_.Position - camera_.At;
			length_ = dir.Length();

			at_ = camera_.At;

			// 初期計算
			ReCalcCamera();
		}


		/// <summary>
		/// 更新処理
		/// </summary>
		public void Update()
		{
			bool is_update = false;
			// 左クリック
			if (Input.IsMouseHold(Input.MouseButton.Left)) {
				is_update = true;

				// マウス操作取得
				Vector2 move = Input.MouseMoveDelta;

				// 回転
				rotation_.X += Lib.Math.DegToRad(-move.Y * ROTATE_UNIT);
				rotation_.X = Lib.Math.Clamp(rotation_.X, MIN_ROTATION_X, MAX_ROTATION_X);
				rotation_.Y += Lib.Math.DegToRad(-move.X * ROTATE_UNIT);

			} else if (Input.IsMouseHold(Input.MouseButton.Right)) {
				// 右クリック
				is_update = true;

				Vector2 move = Input.MouseMoveDelta;
				length_ += move.X * ZOOM_UNIT;
				length_ += -move.Y * ZOOM_UNIT;
				length_ = Math.Max(0.01f, length_);

			} else if (Input.IsMouseHold(Input.MouseButton.Middle)) {
				// ホイール
				is_update = true;

				Vector2 move = Input.MouseMoveDelta;
				Vector3 result;
				Vector3 tmp = camera_.At - camera_.Position;
				tmp.Normalize();
				Vector3 up = camera_.Up;
				Vector3.Cross(ref tmp, ref up, out result);
				result.Normalize();
				Vector3.Cross(ref tmp, ref result, out up);
				up.Normalize();

				at_ -= result * move.X * TRANCE_UNIT;
				at_ -= up * move.Y * TRANCE_UNIT;
			}

			// 更新があった場合位置を再計算
			if (is_update) {
				ReCalcCamera();
			}
		}

		/// <summary>
		/// 位置計算
		/// </summary>
		void ReCalcCamera()
		{
			Quaternion q = Quaternion.RotationYawPitchRoll(rotation_.Y, rotation_.X, 0);

			camera_.SetAt(ref at_);

			Vector4 new_dir = Vector3.Transform(new Vector3(0, 0, 1), q);
			new_dir *= length_;

			Vector3 pos;
			pos.X = new_dir.X + at_.X;
			pos.Y = new_dir.Y + at_.Y;
			pos.Z = new_dir.Z + at_.Z;
			camera_.SetPosition(ref pos);
		}

		/// <summary>
		/// アス比設定
		/// </summary>
		/// <param name="aspect"></param>
		public void SetAspect(float aspect)
		{
			camera_.SetAspect(aspect);
			camera_.Update();
		}
	}
}
