using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Forms;
using System.Windows;
using SlimDX;

namespace Lib
{
	static public class Input
	{
		public enum MouseButton : int
		{
			Left,
			Right,
			Middle,
		}


		// スレッド整合性を保つためのテンポラリ
		static uint mouseButtonTempFlag_;
		static Vector2 mouseMoveTempDelta_;

		static uint mouseButtonFlag_;
		static uint mouseButtonPrevFlag_;
		static Vector2 mousePrevPos_;
		static Vector2 mouseMoveDelta_;


		static public Vector2 MouseMoveDelta { get { return mouseMoveDelta_; } }

		/// <summary>
		/// 
		/// </summary>
		static public void Initialize(Control ctr)
		{
			// ハンドラを登録する
			ctr.MouseDown += MouseKeyDownHandler;
			ctr.MouseUp += MouseKeyUpHandler;
			ctr.MouseMove += MouseMoveHandler;
		}

		/// <summary>
		/// WPF用
		/// </summary>
		/// <param name="element"></param>
		static public void Initialize(UIElement element)
		{
			// ハンドラを登録する
			element.MouseDown += WPFMouseKeyDownHandler;
			element.MouseUp += WPFMouseKeyUpHandler;
			element.MouseMove += WPFMouseMoveHandler;
		}

		static public void Finalize(UIElement element)
		{
			element.MouseDown -= WPFMouseKeyDownHandler;
			element.MouseUp -= WPFMouseKeyUpHandler;
			element.MouseMove -= WPFMouseMoveHandler;
		}


		/// <summary>
		/// 廃棄
		/// </summary>
		static public void Dispose()
		{
		}


		/// <summary>
		/// ムーブハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void MouseMoveHandler(object sender, MouseEventArgs e)
		{
			// 差分
			mouseMoveTempDelta_.X = e.X - mousePrevPos_.X;
			mouseMoveTempDelta_.Y = e.Y - mousePrevPos_.Y;

			// そのフレームのポジション保存
			mousePrevPos_.X = e.X;
			mousePrevPos_.Y = e.Y;
		}


		/// <summary>
		/// キーダウンハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void MouseKeyDownHandler(object sender, MouseEventArgs e)
		{
			switch (e.Button) {
				case MouseButtons.Left:
					mouseButtonTempFlag_ |= 1u << 0;
					break;
				case MouseButtons.Right:
					mouseButtonTempFlag_ |= 1u << 1;
					break;
				case MouseButtons.Middle:
					mouseButtonTempFlag_ |= 1u << 2;
					break;
			}
		}


		/// <summary>
		/// キーアップハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void MouseKeyUpHandler(object sender, MouseEventArgs e)
		{
			switch (e.Button) {
				case MouseButtons.Left:
					mouseButtonTempFlag_ &= ~(1u << 0);
					break;
				case MouseButtons.Right:
					mouseButtonTempFlag_ &= ~(1u << 1);
					break;
				case MouseButtons.Middle:
					mouseButtonTempFlag_ &= ~(1u << 2);
					break;
			}
		}

		/// <summary>
		/// ムーブハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void WPFMouseMoveHandler(object sender, System.Windows.Input.MouseEventArgs e)
		{
			// 差分
			UIElement ui = (UIElement)sender;
			System.Windows.Point p = e.GetPosition(ui);
			mouseMoveTempDelta_.X = (float)p.X - mousePrevPos_.X;
			mouseMoveTempDelta_.Y = (float)p.Y - mousePrevPos_.Y;

			// そのフレームのポジション保存
			mousePrevPos_.X = (float)p.X;
			mousePrevPos_.Y = (float)p.Y;
		}


		/// <summary>
		/// キーダウンハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void WPFMouseKeyDownHandler(object sender, System.Windows.Input.MouseEventArgs e)
		{
			UIElement elem = (UIElement)sender;
			elem.CaptureMouse();

			if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) {
				mouseButtonTempFlag_ |= 1u << 0;
			} else {
				mouseButtonTempFlag_ &= ~(1u << 0);
			}
			if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed) {
				mouseButtonTempFlag_ |= 1u << 1;
			} else {
				mouseButtonTempFlag_ &= ~(1u << 1);
			}
			if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed) {
				mouseButtonTempFlag_ |= 1u << 2;
			} else {
				mouseButtonTempFlag_ &= ~(1u << 2);
			}
		}


		/// <summary>
		/// キーアップハンドラ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		static void WPFMouseKeyUpHandler(object sender, System.Windows.Input.MouseEventArgs e)
		{
			UIElement elem = (UIElement)sender;
			elem.ReleaseMouseCapture();

			if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) {
				mouseButtonTempFlag_ |= 1u << 0;
			} else {
				mouseButtonTempFlag_ &= ~(1u << 0);
			}
			if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed) {
				mouseButtonTempFlag_ |= 1u << 1;
			} else {
				mouseButtonTempFlag_ &= ~(1u << 1);
			}
			if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed) {
				mouseButtonTempFlag_ |= 1u << 2;
			} else {
				mouseButtonTempFlag_ &= ~(1u << 2);
			}
		}

		/// <summary>
		/// 更新処理
		/// </summary>
		static public void Update()
		{
			mouseButtonPrevFlag_ = mouseButtonFlag_;
			mouseButtonFlag_ = mouseButtonTempFlag_;
			mouseMoveDelta_ = mouseMoveTempDelta_;
			mouseMoveTempDelta_ = new Vector2(0, 0);

			// デバッグ出力
			//Console.WriteLine("Mouse / Key : {0}, Move : {1}, {2}", mouseButtonFlag_, mouseMoveDelta_.X, mouseMoveDelta_.Y);
		}


		/// <summary>
		/// マウスの入力があったか
		/// </summary>
		/// <returns></returns>
		static public bool IsMouseInput()
		{
			return mouseButtonFlag_ != 0;
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="button"></param>
		/// <returns></returns>
		static public bool IsMouseHold(MouseButton button)
		{
			return (mouseButtonFlag_ & (1u << (int)button)) != 0;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="button"></param>
		/// <returns></returns>
		static public bool IsMouseTrigger(MouseButton button)
		{
			uint flg = 1u << (int)button;
			return ((mouseButtonFlag_ & flg) != 0) && ((mouseButtonPrevFlag_ & flg) == 0);
		}
	}
}
