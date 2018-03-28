using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Timers;

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    public class KinectHelper
    {
        #region 成员
        //体感设备
        private KinectSensor kinectDriver;
        //骨架数据
        private Skeleton[] frameSkeletons;
        //姿势库
        private readonly Brush[] _SkeletonBrushes = new Brush[] { Brushes.Black, Brushes.Crimson, Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink };
        Window minWindow;
        bool isOK = false;
        bool isKinectControl = false;
        double MoveX;
        double MoveY;
        double Zoon;
        Vector4 handLeft2;
        Vector4 handRight2;

        Timer KinectTimer;

        #endregion 成员

        #region 构造函数
        public KinectHelper(KinectSensor kin, Window win)
        {
            try
            {
                kinectDriver = kin;

                if (kinectDriver != null)
                {
                    minWindow = win;
                    //设置平滑参数
                    TransformSmoothParameters smoothParameters = new TransformSmoothParameters();
                    // 设置处理骨骼数据帧时的平滑量，接受一个0-1的浮点值，值越大，平滑的越多。0表示不进行平滑。
                    smoothParameters.Smoothing = .5f;
                    // 接受一个从0-1的浮点型，值越小，修正越多
                    smoothParameters.Correction = .9f;
                    // 抖动半径，单位为m，如果关节点“抖动”超过了设置的这个半径，将会被纠正到这个半径之内
                    smoothParameters.JitterRadius = 0.05f;
                    // 用来和抖动半径一起来设置抖动半径的最大边界，任何超过这一半径的点都不会认为是抖动产生的，而被认定为是一个新的点。该属性为浮点型，单位为米
                    smoothParameters.MaxDeviationRadius = 0.1f;
                    kinectDriver.SkeletonStream.Enable(smoothParameters);
                    kinectDriver.SkeletonFrameReady += kinectDriver_SkeletonFrameReady;
                    frameSkeletons = new Skeleton[kinectDriver.SkeletonStream.FrameSkeletonArrayLength];
                    kinectDriver.Start();
                }
                KinectTimer = new Timer();
                KinectTimer.Interval = 4000;
                KinectTimer.Elapsed += KinectTimer_Elapsed;

            }
            catch (Exception ex)
            {

                System.Windows.MessageBox.Show(ex.Message);
            }

        }

        void KinectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isOK == false)
            {
                isOK = true;
            }
            else
            {
                isOK = false;
            }
        }

        /// <summary>
        /// 体感设备捕捉到骨架事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void kinectDriver_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            try
            {
                using (SkeletonFrame fram = e.OpenSkeletonFrame())
                {
                    if (fram == null) return;
                    (this.minWindow.FindName("mess") as TextBlock).Text = "骨架流开始了";
                    fram.CopySkeletonDataTo(frameSkeletons);
                    //获取第一位置骨架
                    Skeleton skeleton = GetPrimarySkeleton(frameSkeletons);
                    if (skeleton != null)
                    {

                        ProcessPosePerForming2(skeleton);

                    }


                    for (int i = 0; i < frameSkeletons.Length; i++)
                    {
                        DrawSkeleton(this.frameSkeletons[i], this._SkeletonBrushes[i]);
                    }
                }
            }
            catch (Exception ex)
            {

                System.Windows.MessageBox.Show(ex.Message);
            }
        }
        #endregion


        #region 方法

        /// <summary>
        /// 获取第一位置骨架
        /// </summary>
        /// <param name="frameSkeletons">骨架流</param>
        /// <returns></returns>
        private Skeleton GetPrimarySkeleton(Skeleton[] frameSkeletons)
        {
            Skeleton ske = null;
            try
            {
                for (int i = 0; i < frameSkeletons.Length; i++)
                {
                    if (frameSkeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        if (ske == null)
                        {
                            ske = frameSkeletons[i];
                            (this.minWindow.FindName("mess") as TextBlock).Text = "捕捉到骨架了";
                        }
                        else
                        {
                            if (ske.Position.Z > frameSkeletons[i].Position.Z)
                            {
                                ske = frameSkeletons[i];
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                System.Windows.MessageBox.Show(ex.Message);
            }
            (minWindow.FindName("mess") as TextBlock).Text = "捕捉到骨架";
            return ske;

        }
        /// <summary>
        /// 空间坐标和界面二维坐标转换
        /// </summary>
        /// <param name="joint"><关节/param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public Point GetJointPoint(Joint joint, Point offset)
        {
            //得到节点在UI主界面上的空间位置
            DepthImagePoint point = kinectDriver.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, kinectDriver.DepthStream.Format);
            point.X = (int)(point.X - offset.X);
            point.Y = (int)(point.Y - offset.Y);
            return new Point(point.X, point.Y);
        }
        /// <summary>
        /// 计算2骨骼之间的角度
        /// </summary>
        /// <param name="centerJoint">中心关节点</param>
        /// <param name="angleJoint">角度关节点</param>
        /// <returns></returns>
        public double GetJointAngle(Joint centerJoint, Joint angleJoint)
        {
            double angel = 0;
            double a, b, c;
            Point primaryPoint = GetJointPoint(centerJoint, new Point());
            Point angelPoint = GetJointPoint(angleJoint, new Point());
            Point pr = new Point(primaryPoint.X + angelPoint.X, primaryPoint.Y);
            try
            {
                a = Math.Sqrt(Math.Pow(primaryPoint.X - angelPoint.X, 2) + Math.Pow(primaryPoint.Y - angelPoint.Y, 2));
                b = primaryPoint.X;
                c = Math.Sqrt(Math.Pow(angelPoint.X - pr.X, 2) + Math.Pow(angelPoint.Y - pr.Y, 2));
                double angelRed = Math.Acos((a * a + b * b - c * c) / (2 * a * b));
                angel = angelRed * 180 / Math.PI;
                if (primaryPoint.Y < angelPoint.Y)
                {
                    angel = 360 - angelRed;
                }
            }
            catch (Exception ex)
            {

                System.Windows.MessageBox.Show(ex.Message);
            }

            return angel;
        }
        /// <summary>
        /// 通过3个关节点计算角度
        /// </summary>
        /// <param name="leftJoint">边关节</param>
        /// <param name="centerJoint">中心关节</param>
        /// <param name="rightJoint">边关节</param>
        /// <returns></returns>
        public double GetJointAngle(Joint leftJoint, Joint centerJoint, Joint rightJoint)
        {
            double angel = 0;
            double a, b, c;
            Point primaryPoint = GetJointPoint(leftJoint, new Point());
            Point angelPoint = GetJointPoint(centerJoint, new Point());
            Point pr = GetJointPoint(rightJoint, new Point());
            try
            {
                a = Math.Sqrt(Math.Pow(primaryPoint.X - angelPoint.X, 2) + Math.Pow(primaryPoint.Y - angelPoint.Y, 2));
                b = Math.Sqrt(Math.Pow(angelPoint.X - pr.X, 2) + Math.Pow(angelPoint.Y - pr.Y, 2));
                c = Math.Sqrt(Math.Pow(pr.X - primaryPoint.X, 2) + Math.Pow(pr.Y - primaryPoint.Y, 2));
                double angelRed = Math.Acos((a * a + b * b - c * c) / (2 * a * b));
                angel = angelRed * 180 / Math.PI;
                if (primaryPoint.Y > angelPoint.Y)
                {
                    angel = 360 - angelRed;
                }
            }
            catch (Exception ex)
            {

                System.Windows.MessageBox.Show(ex.Message);
            }

            return angel;
        }
        /// <summary>
        /// 将捕捉到的人体的空间坐标(3维)点转换为计算机界面坐标(2维)
        /// </summary>
        /// <param name="joint">人体关节</param>
        /// <returns></returns>
        private Point GetJointPoint(Joint joint)
        {
            Grid layoutRoot = (minWindow.FindName("layoutGrid") as Grid);
            DepthImagePoint point = this.kinectDriver.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, kinectDriver.DepthStream.Format);
            point.X *= (int)layoutRoot.ActualWidth / kinectDriver.DepthStream.FrameWidth;
            point.Y *= (int)layoutRoot.ActualHeight / kinectDriver.DepthStream.FrameHeight;
            return new Point(point.X, point.Y);
        }
        /// <summary>
        /// 获取关节的4维坐标
        /// </summary>
        /// <param name="joint">关节</param>
        /// <returns></returns>
        private Vector4 GetJointVector4(Joint joint)
        {
            Vector4 v4 = new Vector4();
            Grid layoutRoot = (minWindow.FindName("layoutGrid") as Grid);
            DepthImagePoint point = this.kinectDriver.CoordinateMapper.MapSkeletonPointToDepthPoint(joint.Position, kinectDriver.DepthStream.Format);
            point.X *= (int)layoutRoot.ActualWidth / kinectDriver.DepthStream.FrameWidth;
            point.Y *= (int)layoutRoot.ActualHeight / kinectDriver.DepthStream.FrameHeight;
            v4.X = point.X;
            v4.Y = point.Y;
            v4.Z = joint.Position.Z;
            return v4;
        }

        /// <summary>
        /// 根据人体各种姿态的生理数据判断姿态
        /// 通过关节之间角度，关节之间的相关位置判断姿态
        /// </summary>
        /// <param name="sk">骨架数据</param>
        private void ProcessPosePerForming2(Skeleton sk)
        {
            string mess = "";
            //T型姿态
            //double angelLeft = GetJointAngle(sk.Joints[JointType.ElbowLeft],sk.Joints[JointType.ShoulderLeft]);
            //double angelRight = GetJointAngle(sk.Joints[JointType.ElbowRight],sk.Joints[JointType.ShoulderRight]);
            #region 获取数据
            double angelLeft = GetJointAngle(sk.Joints[JointType.ShoulderLeft], sk.Joints[JointType.ElbowLeft], sk.Joints[JointType.WristLeft]);
            double angelRight = GetJointAngle(sk.Joints[JointType.ShoulderRight], sk.Joints[JointType.ElbowRight], sk.Joints[JointType.WristRight]);

            //胸关节空间位置
            Vector4 shoulderCenter = GetJointVector4(sk.Joints[JointType.ShoulderCenter]);
            Vector4 handLeft = GetJointVector4(sk.Joints[JointType.HandLeft]);
            Vector4 handRight = GetJointVector4(sk.Joints[JointType.HandRight]);
            Vector4 spine = GetJointVector4(sk.Joints[JointType.Spine]);

            double leftCentZ = Math.Round((shoulderCenter.Z - handLeft.Z), 2);
            double rightCenterZ = Math.Round((shoulderCenter.Z - handRight.Z), 2);

            double leftAndRightX = Math.Round(Math.Abs((handRight.X - handLeft.X)), 2);
            double leftAndRightY = Math.Round(Math.Abs((handRight.Y - handLeft.Y)), 2);

            #endregion

            #region 姿势判断
            //T型姿势
            if (leftAndRightX >= 750 && leftAndRightY <= 10 && isOK == false)
            {
                mess = "T型姿势";
                KinectTimer.Start();
                isKinectControl = true;
                //(minWindow.FindName("gridMainMenu") as Grid).Visibility = Visibility.Collapsed;
                //(minWindow.FindName("gridPose") as Grid).Visibility = Visibility.Visible;
                //(minWindow.FindName("gridTuch") as Grid).Visibility = Visibility.Collapsed;
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //}

            }
            else
                //左手举起，右手放下
                if (leftAndRightY > 200 && handLeft.Y < handRight.Y && isOK)
            {
                mess = "左手举起，右手放下";
                //(minWindow.FindName("gridMainMenu") as Grid).Visibility = Visibility.Collapsed;
                //(minWindow.FindName("gridPose") as Grid).Visibility = Visibility.Collapsed;
                //(minWindow.FindName("gridTuch") as Grid).Visibility = Visibility.Visible;
            }
            else
                    //右手举起，左手放下
                    if (leftAndRightY > 200 && handLeft.Y > handRight.Y && isOK)
            {
                mess = "右手举起，左手放下";

            }
            else
                        //双手交叉
                        if ((handRight.X - handLeft.X) < 0 && handLeft.Y < 150 && handRight.Y < 150 && isOK) //handLeft.Y<spine.Y&&handRight.Y<spine.Y
            {
                mess = "双手交叉";
                KinectTimer.Stop();
                isKinectControl = false;
                //(minWindow.FindName("gridMainMenu") as Grid).Visibility = Visibility.Visible;
                //(minWindow.FindName("gridPose") as Grid).Visibility = Visibility.Collapsed;
                //(minWindow.FindName("gridTuch") as Grid).Visibility = Visibility.Collapsed;
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,GUI_FUNC_225_鼠标左键按下");
                //}

            }
            else
                            //双手高举
                            if ((angelRight >= 350 && angelRight <= 380) && (angelLeft >= 350 && angelLeft <= 380))
            {
                mess = "双手高举";
            }
            #endregion

            #region 测试用例
            //(minWindow.FindName("leftX") as TextBlock).Text = "左手X：" + handLeft.X;
            //(minWindow.FindName("leftY") as TextBlock).Text = "左手Y：" + handLeft.Y;
            //(minWindow.FindName("leftZ") as TextBlock).Text = "左手Z：" + handLeft.Z;

            //(minWindow.FindName("rightX") as TextBlock).Text = "右手X：" + handRight.X;
            //(minWindow.FindName("rightY") as TextBlock).Text = "右手Y：" + handRight.Y;
            //(minWindow.FindName("rightZ") as TextBlock).Text = "右手Z：" + handRight.Z;

            //(minWindow.FindName("centerX") as TextBlock).Text = "中心X：" + shoulderCenter.X;
            //(minWindow.FindName("centerY") as TextBlock).Text = "中心Y：" + shoulderCenter.Y;
            //(minWindow.FindName("centerZ") as TextBlock).Text = "中心Z：" + shoulderCenter.Z;
            //(minWindow.FindName("leftAndCenter") as TextBlock).Text = "（脊椎Y-左手Y）=：(" + spine.Y + "-" + handLeft.Y + ")=" + Math.Round((spine.Y - handLeft.Y), 2);
            //(minWindow.FindName("rightAndCenter") as TextBlock).Text = "(脊椎Y-右手Y)=：(" + spine.Y + "-" + handRight.Y + ")=" + Math.Round((spine.Y - handRight.Y), 2);
            //(minWindow.FindName("leftAndCenter") as TextBlock).Text = "（中心Z-左手Z）=：(" + shoulderCenter.Z + "-" + handLeft.Z + ")=" + Math.Round((shoulderCenter.Z - handLeft.Z), 2);
            //(minWindow.FindName("rightAndCenter") as TextBlock).Text = "(右手Z-中心Z)=：(" + shoulderCenter.Z + "-" + handRight.Z + ")=" + Math.Round((shoulderCenter.Z - handRight.Z), 2);
            //(minWindow.FindName("rightAndRightX") as TextBlock).Text = "(左手X-右手X)=：(" + handRight.X + "-" + handLeft.X + ")=" + Math.Round((handRight.X - handLeft.X), 2);//Math.Round(Math.Abs((handRight.X - handLeft.X)), 2);
            //(minWindow.FindName("rightAndLeftY") as TextBlock).Text = "(左手Y-右手Y)绝对值=：(" + handRight.Y + "-" + handLeft.Y + ")=" + Math.Round(Math.Abs((handRight.Y - handLeft.Y)), 2);


            //(minWindow.FindName("leftCenterY") as TextBlock).Text = "（中心Y-左手Y）绝对值=：(" + shoulderCenter.Y + "-" + handLeft.Y + ")=" + Math.Abs(Math.Round((shoulderCenter.Y - handLeft.Y), 2));
            //(minWindow.FindName("rightCenterY") as TextBlock).Text = "（中心Y-右手Y）绝对值=：(" + shoulderCenter.Y + "-" + handLeft.Y + ")=" + Math.Abs(Math.Round((shoulderCenter.Y - handLeft.Y), 2));



            //(minWindow.FindName("els") as Ellipse).Width = Math.Abs((handRight.X - handLeft.X));
            //(minWindow.FindName("els") as Ellipse).Height = Math.Abs((handRight.Y - handLeft.Y));
            (minWindow.FindName("mess") as TextBlock).Text = mess;
            #endregion

            #region 判断姿势执行函数
            string function = "";
            bool isSheck = true;
            if (handRight2 == null)
            {

                handRight2 = handRight;

            }

            #region 计算平滑度
            double maxX;
            double maxY;
            //(minWindow.FindName("rightAndCenter") as TextBlock).Text = "(右手X1-是右手X2)=：(" + handRight.X + "-" + handRight2.X + ")=" + Math.Abs((handRight.X - handRight2.X));
            //(minWindow.FindName("rightAndLeftY") as TextBlock).Text = "(左手Y1-右手Y2)绝对值=：(" + handRight.Y + "-" + handRight2.Y + ")=" + Math.Abs((handRight.Y - handRight2.Y));


            maxX = Math.Abs((handRight.X - handRight2.X));
            maxY = Math.Abs((handRight.Y - handRight2.Y));

            if (maxX > 3 || maxY > 3)
            {
                isSheck = false;
                //(minWindow.FindName("leftCenterY") as TextBlock).Text = "滑动了";
            }
            else
            {
                //(minWindow.FindName("leftCenterY") as TextBlock).Text = "抖动";
                isSheck = true;
            }


            #endregion





            //左手控制
            if (isKinectControl && leftCentZ > 0.4 && rightCenterZ < 0.4)
            {
                double movX = MoveX - handLeft.X;
                double movY = MoveY - handLeft.Y;
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    if (movX < 0)//向右
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 3");
                //    }
                //    else if (movX > 0)//向左
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 4");
                //    }
                //    if (movY > 0)//向上
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 1");

                //    }
                //    else if (movY < 0)//向下
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 2");
                //    }
                //}





                function = "右手控制地图移动X方向：" + handRight.X + "右手控制地图移动Y方向:" + handRight.Y;
                MoveY = handLeft.Y;
                MoveX = handLeft.X;
                function = "左手控制地图移动X方向：" + handLeft.X + "左手控制地图移动Y方向:" + handLeft.Y;
            }
            //右手控制
            if (isKinectControl && leftCentZ < 0.4 && rightCenterZ > 0.4 && !isSheck)
            {
                //(minWindow.FindName("moveX") as TextBlock).Text = "手移动X" + MoveX + "-" + handRight.X + "=" + (MoveX - handRight.X).ToString();
                //(minWindow.FindName("moveY") as TextBlock).Text = "手移动Y" + MoveY + "-" + handRight.Y + "=" + (MoveY - handRight.Y).ToString();
                double movX = MoveX - handRight.X;
                double movY = MoveY - handRight.Y;
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    if (movX < 0)//向右
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 3");
                //    }
                //    else if (movX > 0)//向左
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 4");
                //    }
                //    if (movY > 0)//向上
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 1");

                //    }
                //    else if (movY < 0)//向下
                //    {
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 2");
                //    }
                //}


                function = "右手控制地图移动X方向：" + handRight.X + "右手控制地图移动Y方向:" + handRight.Y;
                MoveY = handRight.Y;
                MoveX = handRight.X;
            }
            if (isKinectControl && leftCentZ < 0 && rightCenterZ < 0)
            {
                function = "地图复位";
            }
            //地图缩放
            if (isKinectControl && leftCentZ < 0 && rightCenterZ > 0)
            {
                function = "地图缩放右手控制：" + rightCenterZ;
            }
            if (isKinectControl && leftCentZ > 0 && rightCenterZ < 0)
            {
                function = "地图缩放左手控制：" + rightCenterZ;
            }
            //双手X放大
            if (isKinectControl && leftCentZ > 0 && leftCentZ < 0.3 && rightCenterZ > 0 && rightCenterZ < 0.3 && leftAndRightX > 300 && !isSheck)
            {
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    if (MoveX - leftAndRightX > 0)//缩小
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 2";
                //        // (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);

                //    }
                //    if (MoveX - leftAndRightX < 0)//放大
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 1";
                //        //  (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //    }
                //}


                //(minWindow.FindName("moveX") as TextBlock).Text = "双手放大X" + MoveX + "-" + leftAndRightX + "=" + (MoveX - leftAndRightX).ToString();

                MoveX = leftAndRightX;
            }
            //双手Y放大
            if (isKinectControl && leftCentZ > 0 && leftCentZ < 0.3 && rightCenterZ > 0 && rightCenterZ < 0.3 && leftAndRightY > 100 && !isSheck)
            {
                function = "地图放大双手控制Y：" + leftAndRightX;
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    if (MoveY - leftAndRightY > 0)// 缩小
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 2";
                //        //   (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //    }
                //    if (MoveY - leftAndRightY < 0)//放大
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 1";
                //        // (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //    }
                //}
                //(minWindow.FindName("moveY") as TextBlock).Text = "双手缩放Y" + MoveY + "-" + leftAndRightY + "=" + (MoveY - leftAndRightY).ToString();
                MoveY = leftAndRightY;
            }
            //双手放大
            if (isKinectControl && leftCentZ > 0.4 && rightCenterZ > 0.4 && leftAndRightY > 80 && !isSheck)
            {
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    string fun1 = "设置相机只能水平移动, 0";
                //    string fun2 = "相机移动控制, 0, 1";
                //    // (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //}
                function = "地图缩小控制X：" + leftAndRightY;
            }
            //双缩小
            if (isKinectControl && leftCentZ > 0.4 && rightCenterZ > 0.4 && leftAndRightX > 600)
            {
                //if ((this.minWindow as MainMenu).mainWindow != null)
                //{
                //    string fun1 = "设置相机只能水平移动, 0";
                //    string fun2 = "相机移动控制, 0, 2";
                //    //   (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //    (this.minWindow as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //}
            }
            //(minWindow.FindName("ctiveInfo") as TextBlock).Text = function;
            #endregion


            handRight2 = handRight;


        }
        /// <summary>
        /// 绘画人体当前骨架
        /// </summary>
        /// <param name="skeleton">骨架数据</param>
        /// <param name="brush">画笔</param>
        private void DrawSkeleton(Skeleton skeleton, Brush brush)
        {
            Grid SkeletonsPanel = (minWindow.FindName("SkeletonsPanel") as Grid);
            SkeletonsPanel.Children.Clear();
            if (skeleton != null && skeleton.TrackingState == SkeletonTrackingState.Tracked)
            {
                //绘制头部和躯干部
                Polyline figure = CreateFigure(skeleton, brush, new[] { JointType.Head, JointType.ShoulderCenter, JointType.ShoulderLeft, JointType.Spine,
                                                                             JointType.ShoulderRight, JointType.ShoulderCenter, JointType.HipCenter,
                                                                             JointType.HipLeft, JointType.Spine, JointType.HipRight, JointType.HipCenter});
                SkeletonsPanel.Children.Add(figure);
                //绘画左脚
                figure = CreateFigure(skeleton, brush, new[] { JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft });
                SkeletonsPanel.Children.Add(figure);

                //画右脚
                figure = CreateFigure(skeleton, brush, new[] { JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight });
                SkeletonsPanel.Children.Add(figure);

                //h画左臂
                figure = CreateFigure(skeleton, brush, new[] { JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft });
                SkeletonsPanel.Children.Add(figure);

                //画右臂
                figure = CreateFigure(skeleton, brush, new[] { JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight });
                SkeletonsPanel.Children.Add(figure);
            }
        }
        /// <summary>
        /// 根据人体骨架绘制多线段
        /// </summary>
        /// <param name="skeleton">骨架数据</param>
        /// <param name="brush">画笔</param>
        /// <param name="joints">关节</param>
        /// <returns>多线段</returns>
        private Polyline CreateFigure(Skeleton skeleton, Brush brush, JointType[] joints)
        {
            Polyline figure = new Polyline();

            figure.StrokeThickness = 18;
            figure.Stroke = brush;

            for (int i = 0; i < joints.Length; i++)
            {
                figure.Points.Add(GetJointPoint(skeleton.Joints[joints[i]]));
            }

            return figure;
        }
        #endregion
    }
}