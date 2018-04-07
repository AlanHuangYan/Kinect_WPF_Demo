//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Controls;
    using System;
    using System.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 成员
        //体感设备
        private KinectSensor kinectDriver;
        //骨架数据
        private Skeleton[] frameSkeletons;
        //姿势库
        private readonly Brush[] _SkeletonBrushes = new Brush[] { Brushes.Black, Brushes.Crimson, Brushes.Indigo, Brushes.DodgerBlue, Brushes.Purple, Brushes.Pink };
        bool isOK = false;
        bool isKinectControl = false;
        double MoveX;
        double MoveY;
        double Zoon;
        Vector4 handLeft2;
        Vector4 handRight2;

        Timer KinectTimer;

        #endregion 成员

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        KinectHelper helper;
        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    
                    break;
                }
            }

            if (null != this.sensor)
            {
                //helper = new KinectHelper(sensor, this);

                kinectDriver = sensor;
                isOK = true;

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;



                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }
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
                            (this.FindName("mess") as TextBlock).Text = "捕捉到骨架了";
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
            (this.FindName("mess") as TextBlock).Text = "捕捉到骨架";
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
            Grid layoutRoot = (this.FindName("layoutGrid") as Grid);
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
            Grid layoutRoot = (this.FindName("layoutGrid") as Grid);
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

            (this.FindName("leftX") as TextBlock).Text = handLeft.X.ToString() + ", " ;
            (this.FindName("leftY") as TextBlock).Text = handLeft.Y.ToString() + ", ";
            (this.FindName("rightX") as TextBlock).Text = handRight.X.ToString() + ", ";
            (this.FindName("rightY") as TextBlock).Text = handRight.Y.ToString() + ", ";

            (this.FindName("leftAndRightX") as TextBlock).Text = leftAndRightX.ToString() + ", ";
            (this.FindName("leftAndRightY") as TextBlock).Text = leftAndRightY.ToString() + ", ";

            #endregion

            #region 姿势判断
            //T型姿势
            if (leftAndRightX >= 750 && leftAndRightY <= 10 && isOK == false)
            {
                mess = "T型姿势";
                //KinectTimer.Start();
                isKinectControl = true;
                //(this.FindName("gridMainMenu") as Grid).Visibility = Visibility.Collapsed;
                //(this.FindName("gridPose") as Grid).Visibility = Visibility.Visible;
                //(this.FindName("gridTuch") as Grid).Visibility = Visibility.Collapsed;
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //}

            }
            else
                //左手举起，右手放下
                if (leftAndRightY > 200 && handLeft.Y < handRight.Y && isOK)
            {
                mess = "左手举起，右手放下";
                //(this.FindName("gridMainMenu") as Grid).Visibility = Visibility.Collapsed;
                //(this.FindName("gridPose") as Grid).Visibility = Visibility.Collapsed;
                //(this.FindName("gridTuch") as Grid).Visibility = Visibility.Visible;
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
                //KinectTimer.Stop();
                isKinectControl = false;
                //(this.FindName("gridMainMenu") as Grid).Visibility = Visibility.Visible;
                //(this.FindName("gridPose") as Grid).Visibility = Visibility.Collapsed;
                //(this.FindName("gridTuch") as Grid).Visibility = Visibility.Collapsed;
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,GUI_FUNC_225_鼠标左键按下");
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
            (this.FindName("leftX") as TextBlock).Text = "左手X：" + handLeft.X;
            (this.FindName("leftY") as TextBlock).Text = "左手Y：" + handLeft.Y;
            (this.FindName("leftZ") as TextBlock).Text = "左手Z：" + handLeft.Z;

            (this.FindName("rightX") as TextBlock).Text = "右手X：" + handRight.X;
            (this.FindName("rightY") as TextBlock).Text = "右手Y：" + handRight.Y;
            (this.FindName("rightZ") as TextBlock).Text = "右手Z：" + handRight.Z;

            (this.FindName("centerX") as TextBlock).Text = "中心X：" + shoulderCenter.X;
            (this.FindName("centerY") as TextBlock).Text = "中心Y：" + shoulderCenter.Y;
            (this.FindName("centerZ") as TextBlock).Text = "中心Z：" + shoulderCenter.Z;
            (this.FindName("leftAndCenter") as TextBlock).Text = "（脊椎Y-左手Y）=：(" + spine.Y + "-" + handLeft.Y + ")=" + Math.Round((spine.Y - handLeft.Y), 2);
            (this.FindName("rightAndCenter") as TextBlock).Text = "(脊椎Y-右手Y)=：(" + spine.Y + "-" + handRight.Y + ")=" + Math.Round((spine.Y - handRight.Y), 2);
            (this.FindName("centerZAndLeftZ") as TextBlock).Text = "（中心Z-左手Z）=：(" + shoulderCenter.Z + "-" + handLeft.Z + ")=" + Math.Round((shoulderCenter.Z - handLeft.Z), 2);
            (this.FindName("rightZAndCenterZ") as TextBlock).Text = "(右手Z-中心Z)=：(" + shoulderCenter.Z + "-" + handRight.Z + ")=" + Math.Round((shoulderCenter.Z - handRight.Z), 2);
            (this.FindName("leftXAndRightX") as TextBlock).Text = "(左手X-右手X)=：(" + handRight.X + "-" + handLeft.X + ")=" + Math.Round((handRight.X - handLeft.X), 2);//Math.Round(Math.Abs((handRight.X - handLeft.X)), 2);
            (this.FindName("leftYAndRightY") as TextBlock).Text = "(左手Y-右手Y)绝对值=：(" + handRight.Y + "-" + handLeft.Y + ")=" + Math.Round(Math.Abs((handRight.Y - handLeft.Y)), 2);


            (this.FindName("leftCenterY") as TextBlock).Text = "（中心Y-左手Y）绝对值=：(" + shoulderCenter.Y + "-" + handLeft.Y + ")=" + Math.Abs(Math.Round((shoulderCenter.Y - handLeft.Y), 2));
            (this.FindName("rightCenterY") as TextBlock).Text = "（中心Y-右手Y）绝对值=：(" + shoulderCenter.Y + "-" + handLeft.Y + ")=" + Math.Abs(Math.Round((shoulderCenter.Y - handLeft.Y), 2));



            //(this.FindName("els") as Ellipse).Width = Math.Abs((handRight.X - handLeft.X));
            //(this.FindName("els") as Ellipse).Height = Math.Abs((handRight.Y - handLeft.Y));
            (this.FindName("mess") as TextBlock).Text = mess;
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
            //(this.FindName("rightAndCenter") as TextBlock).Text = "(右手X1-是右手X2)=：(" + handRight.X + "-" + handRight2.X + ")=" + Math.Abs((handRight.X - handRight2.X));
            //(this.FindName("rightAndLeftY") as TextBlock).Text = "(左手Y1-右手Y2)绝对值=：(" + handRight.Y + "-" + handRight2.Y + ")=" + Math.Abs((handRight.Y - handRight2.Y));


            maxX = Math.Abs((handRight.X - handRight2.X));
            maxY = Math.Abs((handRight.Y - handRight2.Y));

            if (maxX > 3 || maxY > 3)
            {
                isSheck = false;
                (this.FindName("leftCenterY_ss") as TextBlock).Text = "滑动了";
            }
            else
            {
                (this.FindName("leftCenterY") as TextBlock).Text = "抖动";
                isSheck = true;
            }


            #endregion


            //左手控制
            if (isKinectControl && leftCentZ > 0.4 && rightCenterZ < 0.4)
            {
                double movX = MoveX - handLeft.X;
                double movY = MoveY - handLeft.Y;
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    if (movX < 0)//向右
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 3");
                //    }
                //    else if (movX > 0)//向左
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 4");
                //    }
                //    if (movY > 0)//向上
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 1");

                //    }
                //    else if (movY < 0)//向下
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 2");
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
                //(this.FindName("moveX") as TextBlock).Text = "手移动X" + MoveX + "-" + handRight.X + "=" + (MoveX - handRight.X).ToString();
                //(this.FindName("moveY") as TextBlock).Text = "手移动Y" + MoveY + "-" + handRight.Y + "=" + (MoveY - handRight.Y).ToString();
                double movX = MoveX - handRight.X;
                double movY = MoveY - handRight.Y;
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    if (movX < 0)//向右
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 3");
                //    }
                //    else if (movX > 0)//向左
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 4");
                //    }
                //    if (movY > 0)//向上
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 1");

                //    }
                //    else if (movY < 0)//向下
                //    {
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("相机移动控制, 0, 2");
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
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    if (MoveX - leftAndRightX > 0)//缩小
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 2";
                //        // (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);

                //    }
                //    if (MoveX - leftAndRightX < 0)//放大
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 1";
                //        //  (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //    }
                //}


                //(this.FindName("moveX") as TextBlock).Text = "双手放大X" + MoveX + "-" + leftAndRightX + "=" + (MoveX - leftAndRightX).ToString();

                MoveX = leftAndRightX;
            }
            //双手Y放大
            if (isKinectControl && leftCentZ > 0 && leftCentZ < 0.3 && rightCenterZ > 0 && rightCenterZ < 0.3 && leftAndRightY > 100 && !isSheck)
            {
                function = "地图放大双手控制Y：" + leftAndRightX;
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    if (MoveY - leftAndRightY > 0)// 缩小
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 2";
                //        //   (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //    }
                //    if (MoveY - leftAndRightY < 0)//放大
                //    {
                //        string fun1 = "设置相机只能水平移动, 0";
                //        string fun2 = "相机移动控制, 0, 1";
                //        // (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //        (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //    }
                //}
                //(this.FindName("moveY") as TextBlock).Text = "双手缩放Y" + MoveY + "-" + leftAndRightY + "=" + (MoveY - leftAndRightY).ToString();
                MoveY = leftAndRightY;
            }
            //双手放大
            if (isKinectControl && leftCentZ > 0.4 && rightCenterZ > 0.4 && leftAndRightY > 80 && !isSheck)
            {
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    string fun1 = "设置相机只能水平移动, 0";
                //    string fun2 = "相机移动控制, 0, 1";
                //    // (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //}
                function = "地图缩小控制X：" + leftAndRightY;
            }
            //双缩小
            if (isKinectControl && leftCentZ > 0.4 && rightCenterZ > 0.4 && leftAndRightX > 600)
            {
                //if ((this as MainMenu).mainWindow != null)
                //{
                //    string fun1 = "设置相机只能水平移动, 0";
                //    string fun2 = "相机移动控制, 0, 2";
                //    //   (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript("执行内部函数,funReset");
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun1);
                //    (this as MainMenu).mainWindow.VRPControl.ExecuteVrpScript(fun2);
                //}
            }
            //(this.FindName("ctiveInfo") as TextBlock).Text = function;
            #endregion


            handRight2 = handRight;


        }
        
        #endregion



        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    (this.FindName("mess") as TextBlock).Text = "骨架流开始了";
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);

                    //获取第一位置骨架
                    Skeleton skeleton = GetPrimarySkeleton(skeletons);
                    if (skeleton != null)
                    {

                        ProcessPosePerForming2(skeleton);

                    }

                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    var point = this.SkeletonPointToScreen(joint.Position);
                    drawingContext.DrawEllipse(drawBrush, null, point, JointThickness, JointThickness);

                    //drawingContext.DrawEllipse(drawBrush, null, new Point(point.X + 20, point.Y + 20), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));

        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

    }
}