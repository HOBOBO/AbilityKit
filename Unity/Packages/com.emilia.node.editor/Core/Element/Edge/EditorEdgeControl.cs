using System;
using System.Collections.Generic;
using Emilia.Reflection.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 重写EdgeControl
    /// </summary>
    public class EditorEdgeControl : EdgeControl_Internals
    {
        protected struct EdgeCornerSweepValues
        {
            public Vector2 circleCenter;
            public double sweepAngle;
            public double startAngle;
            public double endAngle;
            public Vector2 crossPoint1;
            public Vector2 crossPoint2;
            public float radius;
        }

        protected const float EdgeLengthFromPort = 12.0f;
        protected const float EdgeTurnDiameter = 16.0f;
        protected const float EdgeSweepResampleRatio = 4.0f;
        protected const int EdgeStraightLineSegmentDivisor = 5;

        protected EditorOrientation _inputEditorOrientation;
        protected EditorOrientation _outputEditorOrientation;
        protected bool _disabledEdgeDrawOptimization;

        /// <summary>
        /// 禁用边绘制优化
        /// </summary>
        public bool disabledEdgeDrawOptimization
        {
            get => _disabledEdgeDrawOptimization;
            set => _disabledEdgeDrawOptimization = value;
        }

        /// <summary>
        /// 输入方向
        /// </summary>
        public virtual EditorOrientation inputEditorOrientation
        {
            get => this._inputEditorOrientation;
            set
            {
                if (this._inputEditorOrientation == value) return;
                this._inputEditorOrientation = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// 输出方向
        /// </summary>
        public virtual EditorOrientation outputEditorOrientation
        {
            get => this._outputEditorOrientation;
            set
            {
                if (this._outputEditorOrientation == value) return;
                this._outputEditorOrientation = value;
                MarkDirtyRepaint();
            }
        }

        protected List<Vector2> lastLocalControlPoints = new();

        protected override void UpdateRenderPoints()
        {
            ComputeControlPoints();

            if (renderPointsDirty_Internals == false && controlPoints != null) return;

            Vector2 p1 = parent.ChangeCoordinatesTo(this, controlPoints[0]);
            Vector2 p2 = parent.ChangeCoordinatesTo(this, controlPoints[1]);
            Vector2 p3 = parent.ChangeCoordinatesTo(this, controlPoints[2]);
            Vector2 p4 = parent.ChangeCoordinatesTo(this, controlPoints[3]);

            if (lastLocalControlPoints.Count == 4)
            {
                if (Approximately(p1, lastLocalControlPoints[0]) &&
                    Approximately(p2, lastLocalControlPoints[1]) &&
                    Approximately(p3, lastLocalControlPoints[2]) &&
                    Approximately(p4, lastLocalControlPoints[3]))
                {
                    renderPointsDirty_Internals = false;
                    return;
                }
            }

            lastLocalControlPoints.Clear();
            lastLocalControlPoints.Add(p1);
            lastLocalControlPoints.Add(p2);
            lastLocalControlPoints.Add(p3);
            lastLocalControlPoints.Add(p4);

            renderPoints_Internals.Clear();

            //当Orientation为Custom的处理
            if (inputEditorOrientation == EditorOrientation.Custom || outputEditorOrientation == EditorOrientation.Custom)
            {
                renderPoints_Internals.Add(p1);
                renderPoints_Internals.Add(p4);
                return;
            }

            // 非自然连接优化（电路图样式）
            if (disabledEdgeDrawOptimization == false && IsUnnaturalConnection(p1, p4, p2, p3))
            {
                RenderCircuitStyleConnection(p1, p4, p2, p3);
                renderPointsDirty_Internals = false;
                return;
            }

            float diameter = EdgeTurnDiameter;

            bool sameOrientations = inputEditorOrientation == outputEditorOrientation;
            if (sameOrientations &&
                ((outputEditorOrientation == EditorOrientation.Horizontal && Mathf.Abs(p1.y - p4.y) < 2 && p1.x + EdgeLengthFromPort < p4.x - EdgeLengthFromPort) ||
                 (outputEditorOrientation == EditorOrientation.Vertical && Mathf.Abs(p1.x - p4.x) < 2 && p1.y + EdgeLengthFromPort < p4.y - EdgeLengthFromPort)))
            {
                RenderStraightLines(p1, p2, p3, p4);
                return;
            }

            bool renderBothCorners = true;

            EdgeCornerSweepValues corner1 = GetCornerSweepValues(p1, p2, p3, diameter, Direction.Output);
            EdgeCornerSweepValues corner2 = GetCornerSweepValues(p2, p3, p4, diameter, Direction.Input);

            if (! ValidateCornerSweepValues(ref corner1, ref corner2))
            {
                if (sameOrientations)
                {
                    RenderStraightLines(p1, p2, p3, p4);
                    return;
                }

                renderBothCorners = false;

                Vector2 px = outputOrientation == Orientation.Horizontal ? new Vector2(p4.x, p1.y) : new Vector2(p1.x, p4.y);

                corner1 = GetCornerSweepValues(p1, px, p4, diameter, Direction.Output);
            }

            renderPoints_Internals.Add(p1);

            if (! sameOrientations && renderBothCorners)
            {
                float minDistance = 2 * diameter * diameter;
                if ((p3 - p2).sqrMagnitude < minDistance ||
                    (p4 - p1).sqrMagnitude < minDistance)
                {
                    Vector2 px = (p2 + p3) * 0.5f;
                    corner1 = GetCornerSweepValues(p1, px, p4, diameter, Direction.Output);
                    renderBothCorners = false;
                }
            }

            GetRoundedCornerPoints(renderPoints_Internals, corner1, Direction.Output);
            if (renderBothCorners) GetRoundedCornerPoints(renderPoints_Internals, corner2, Direction.Input);

            renderPoints_Internals.Add(p4);
        }

        protected void RenderStraightLines(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float safeSpan = outputOrientation == Orientation.Horizontal
                ? Mathf.Abs(p1.x + EdgeLengthFromPort - (p4.x - EdgeLengthFromPort))
                : Mathf.Abs(p1.y + EdgeLengthFromPort - (p4.y - EdgeLengthFromPort));

            float safeSpan3 = safeSpan / EdgeStraightLineSegmentDivisor;
            float nodeToP2Dist = Mathf.Min(safeSpan3, EdgeTurnDiameter);
            nodeToP2Dist = Mathf.Max(0, nodeToP2Dist);

            Vector2 offset = outputOrientation == Orientation.Horizontal
                ? new Vector2(EdgeTurnDiameter - nodeToP2Dist, 0)
                : new Vector2(0, EdgeTurnDiameter - nodeToP2Dist);

            renderPoints_Internals.Add(p1);
            renderPoints_Internals.Add(p2 - offset);
            renderPoints_Internals.Add(p3 + offset);
            renderPoints_Internals.Add(p4);
        }

        protected bool ValidateCornerSweepValues(ref EdgeCornerSweepValues corner1, ref EdgeCornerSweepValues corner2)
        {
            Vector2 circlesMidpoint = (corner1.circleCenter + corner2.circleCenter) / 2;

            Vector2 p2CenterToCross1 = corner1.circleCenter - corner1.crossPoint1;
            Vector2 p2CenterToCirclesMid = corner1.circleCenter - circlesMidpoint;
            double angleToCirclesMid = outputOrientation == Orientation.Horizontal
                ? Math.Atan2(p2CenterToCross1.y, p2CenterToCross1.x) - Math.Atan2(p2CenterToCirclesMid.y, p2CenterToCirclesMid.x)
                : Math.Atan2(p2CenterToCross1.x, p2CenterToCross1.y) - Math.Atan2(p2CenterToCirclesMid.x, p2CenterToCirclesMid.y);

            if (double.IsNaN(angleToCirclesMid)) return false;

            angleToCirclesMid = Math.Sign(angleToCirclesMid) * 2 * Mathf.PI - angleToCirclesMid;
            if (Mathf.Abs((float) angleToCirclesMid) > 1.5 * Mathf.PI) angleToCirclesMid = -1 * Math.Sign(angleToCirclesMid) * 2 * Mathf.PI + angleToCirclesMid;

            float h = p2CenterToCirclesMid.magnitude;
            float p2AngleToMidTangent = Mathf.Acos(corner1.radius / h);

            if (double.IsNaN(p2AngleToMidTangent)) return false;

            float maxSweepAngle = Mathf.Abs((float) corner1.sweepAngle) - p2AngleToMidTangent * 2;

            if (Mathf.Abs((float) angleToCirclesMid) < Mathf.Abs((float) corner1.sweepAngle))
            {
                corner1.sweepAngle = Math.Sign(corner1.sweepAngle) * Mathf.Min(maxSweepAngle, Mathf.Abs((float) corner1.sweepAngle));
                corner2.sweepAngle = Math.Sign(corner2.sweepAngle) * Mathf.Min(maxSweepAngle, Mathf.Abs((float) corner2.sweepAngle));
            }

            return true;
        }

        protected EdgeCornerSweepValues GetCornerSweepValues(
            Vector2 p1, Vector2 cornerPoint, Vector2 p2, float diameter, Direction closestPortDirection)
        {
            EdgeCornerSweepValues corner = new();

            corner.radius = diameter / 2;

            Vector2 d1Corner = (cornerPoint - p1).normalized;
            Vector2 d1 = d1Corner * diameter;
            float dx1 = d1.x;
            float dy1 = d1.y;

            Vector2 d2Corner = (cornerPoint - p2).normalized;
            Vector2 d2 = d2Corner * diameter;
            float dx2 = d2.x;
            float dy2 = d2.y;

            float angle = (float) (Math.Atan2(dy1, dx1) - Math.Atan2(dy2, dx2)) / 2;

            float tan = (float) Math.Abs(Math.Tan(angle));
            float segment = corner.radius / tan;

            if (segment > diameter)
            {
                segment = diameter;
                corner.radius = diameter * tan;
            }

            corner.crossPoint1 = cornerPoint - d1Corner * segment;
            corner.crossPoint2 = cornerPoint - d2Corner * segment;

            corner.circleCenter = GetCornerCircleCenter(cornerPoint, corner.crossPoint1, corner.crossPoint2, segment, corner.radius);

            corner.startAngle = Math.Atan2(corner.crossPoint1.y - corner.circleCenter.y, corner.crossPoint1.x - corner.circleCenter.x);
            corner.endAngle = Math.Atan2(corner.crossPoint2.y - corner.circleCenter.y, corner.crossPoint2.x - corner.circleCenter.x);

            corner.sweepAngle = corner.endAngle - corner.startAngle;

            if (closestPortDirection == Direction.Input)
            {
                double endAngle = corner.endAngle;
                corner.endAngle = corner.startAngle;
                corner.startAngle = endAngle;
            }

            if (corner.sweepAngle > Math.PI) corner.sweepAngle = -2 * Math.PI + corner.sweepAngle;
            else if (corner.sweepAngle < -Math.PI) corner.sweepAngle = 2 * Math.PI + corner.sweepAngle;

            return corner;
        }

        protected Vector2 GetCornerCircleCenter(Vector2 cornerPoint, Vector2 crossPoint1, Vector2 crossPoint2, float segment, float radius)
        {
            float dx = cornerPoint.x * 2 - crossPoint1.x - crossPoint2.x;
            float dy = cornerPoint.y * 2 - crossPoint1.y - crossPoint2.y;

            var cornerToCenterVector = new Vector2(dx, dy);

            float L = cornerToCenterVector.magnitude;

            if (Mathf.Approximately(L, 0))
            {
                return cornerPoint;
            }

            float d = new Vector2(segment, radius).magnitude;
            float factor = d / L;

            return new Vector2(cornerPoint.x - cornerToCenterVector.x * factor, cornerPoint.y - cornerToCenterVector.y * factor);
        }

        protected void GetRoundedCornerPoints(List<Vector2> points, EdgeCornerSweepValues corner, Direction closestPortDirection)
        {
            int pointsCount = Mathf.CeilToInt((float) Math.Abs(corner.sweepAngle * EdgeSweepResampleRatio));
            int sign = Math.Sign(corner.sweepAngle);
            bool backwards = closestPortDirection == Direction.Input;

            for (int i = 0; i < pointsCount; ++i)
            {
                float sweepIndex = backwards ? i - pointsCount : i;

                double sweepedAngle = corner.startAngle + sign * sweepIndex / EdgeSweepResampleRatio;

                var pointX = (float) (corner.circleCenter.x + Math.Cos(sweepedAngle) * corner.radius);
                var pointY = (float) (corner.circleCenter.y + Math.Sin(sweepedAngle) * corner.radius);

                if (i == 0 && backwards)
                {
                    if (outputOrientation == Orientation.Horizontal)
                    {
                        if (corner.sweepAngle < 0 && points[points.Count - 1].y > pointY) continue;
                        else if (corner.sweepAngle >= 0 && points[points.Count - 1].y < pointY) continue;
                    }
                    else
                    {
                        if (corner.sweepAngle < 0 && points[points.Count - 1].x < pointX) continue;
                        else if (corner.sweepAngle >= 0 && points[points.Count - 1].x > pointX) continue;
                    }
                }

                points.Add(new Vector2(pointX, pointY));
            }
        }

        protected static bool Approximately(Vector2 v1, Vector2 v2) => Mathf.Approximately(v1.x, v2.x) && Mathf.Approximately(v1.y, v2.y);

        /// <summary>
        /// 判断是否为非自然连接（端口朝向与目标方向相反）
        /// </summary>
        protected bool IsUnnaturalConnection(Vector2 from, Vector2 to, Vector2 outputControlPoint, Vector2 inputControlPoint)
        {
            if (outputEditorOrientation == EditorOrientation.Horizontal)
            {
                // 使用控制点判断输出端口实际朝向
                float outputDir = Mathf.Sign(outputControlPoint.x - from.x);
                float targetDir = Mathf.Sign(to.x - from.x);

                // 如果输出方向与目标方向相反，则为非自然连接
                if (outputDir != targetDir && Mathf.Abs(to.x - from.x) > EdgeLengthFromPort)
                {
                    return true;
                }
            }
            else if (outputEditorOrientation == EditorOrientation.Vertical)
            {
                float outputDir = Mathf.Sign(outputControlPoint.y - from.y);
                float targetDir = Mathf.Sign(to.y - from.y);

                if (outputDir != targetDir && Mathf.Abs(to.y - from.y) > EdgeLengthFromPort)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 渲染电路图样式连接（带圆角）
        /// </summary>
        protected void RenderCircuitStyleConnection(Vector2 p1, Vector2 p4, Vector2 outputControlPoint, Vector2 inputControlPoint)
        {
            const float cornerRadius = 8f;
            float diameter = cornerRadius * 2;
            float extendDistance = EdgeLengthFromPort + EdgeTurnDiameter;

            // 使用控制点确定输出和输入的延伸方向
            Vector2 outputDir = (outputControlPoint - p1).normalized;
            Vector2 inputDir = (inputControlPoint - p4).normalized;

            // 计算输出延伸点
            Vector2 extend1 = p1 + outputDir * extendDistance;

            // 计算输入延伸点
            Vector2 extend2 = p4 + inputDir * extendDistance;

            // 计算中间高度/宽度
            float midY, midX;
            if (outputEditorOrientation == EditorOrientation.Horizontal)
            {
                midY = (p1.y + p4.y) / 2;
                // 如果Y坐标太接近，增加偏移避免折线重叠
                if (Mathf.Abs(p1.y - p4.y) < cornerRadius * 6)
                {
                    midY = Mathf.Min(p1.y, p4.y) - cornerRadius * 4;
                }
                midX = (extend1.x + extend2.x) / 2;

                // 水平方向的电路图路径
                RenderHorizontalCircuitPath(p1, p4, extend1, extend2, midY, diameter);
            }
            else
            {
                midX = (p1.x + p4.x) / 2;
                if (Mathf.Abs(p1.x - p4.x) < cornerRadius * 6)
                {
                    midX = Mathf.Min(p1.x, p4.x) - cornerRadius * 4;
                }
                midY = (extend1.y + extend2.y) / 2;

                // 垂直方向的电路图路径
                RenderVerticalCircuitPath(p1, p4, extend1, extend2, midX, diameter);
            }
        }

        /// <summary>
        /// 渲染水平方向的电路图路径
        /// </summary>
        protected void RenderHorizontalCircuitPath(Vector2 p1, Vector2 p4, Vector2 extend1, Vector2 extend2, float midY, float diameter)
        {
            float radius = diameter / 2;

            // 构建路径的关键点
            Vector2 corner1 = new Vector2(extend1.x, p1.y);      // 第一个拐角
            Vector2 corner2 = new Vector2(extend1.x, midY);      // 第二个拐角
            Vector2 corner3 = new Vector2(extend2.x, midY);      // 第三个拐角
            Vector2 corner4 = new Vector2(extend2.x, p4.y);      // 第四个拐角

            // 添加起点
            renderPoints_Internals.Add(p1);

            // 第一个圆角：水平转垂直
            float dir1X = Mathf.Sign(corner1.x - p1.x);          // 水平方向
            float dir1Y = Mathf.Sign(corner2.y - corner1.y);     // 垂直方向
            AddRoundedCorner(corner1, radius, dir1X, 0, 0, dir1Y);

            // 第二个圆角：垂直转水平
            float dir2X = Mathf.Sign(corner3.x - corner2.x);     // 水平方向
            float dir2Y = Mathf.Sign(corner2.y - corner1.y);     // 垂直方向（从上一段来）
            AddRoundedCorner(corner2, radius, 0, dir2Y, dir2X, 0);

            // 第三个圆角：水平转垂直
            float dir3X = Mathf.Sign(corner3.x - corner2.x);     // 水平方向（从上一段来）
            float dir3Y = Mathf.Sign(corner4.y - corner3.y);     // 垂直方向
            AddRoundedCorner(corner3, radius, dir3X, 0, 0, dir3Y);

            // 第四个圆角：垂直转水平
            float dir4X = Mathf.Sign(p4.x - corner4.x);          // 水平方向
            float dir4Y = Mathf.Sign(corner4.y - corner3.y);     // 垂直方向（从上一段来）
            AddRoundedCorner(corner4, radius, 0, dir4Y, dir4X, 0);

            // 添加终点
            renderPoints_Internals.Add(p4);
        }

        /// <summary>
        /// 添加圆角点
        /// </summary>
        /// <param name="corner">拐角位置</param>
        /// <param name="radius">圆角半径</param>
        /// <param name="inDirX">进入方向X分量（-1, 0, 1）</param>
        /// <param name="inDirY">进入方向Y分量（-1, 0, 1）</param>
        /// <param name="outDirX">离开方向X分量（-1, 0, 1）</param>
        /// <param name="outDirY">离开方向Y分量（-1, 0, 1）</param>
        protected void AddRoundedCorner(Vector2 corner, float radius, float inDirX, float inDirY, float outDirX, float outDirY)
        {
            // 计算圆心位置：corner + (-inDir + outDir) * radius
            Vector2 center = corner + new Vector2((-inDirX + outDirX) * radius, (-inDirY + outDirY) * radius);

            // 入口切点相对于圆心的方向是 -outDir
            // 出口切点相对于圆心的方向是 inDir
            float startAngle = Mathf.Atan2(-outDirY, -outDirX);
            float endAngle = Mathf.Atan2(inDirY, inDirX);

            // 计算扫描角度，确保是较短的弧
            float sweepAngle = endAngle - startAngle;
            if (sweepAngle > Mathf.PI) sweepAngle -= 2 * Mathf.PI;
            if (sweepAngle < -Mathf.PI) sweepAngle += 2 * Mathf.PI;

            // 生成弧线上的点
            int segments = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(sweepAngle) * 4));
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float) segments;
                float angle = startAngle + sweepAngle * t;
                renderPoints_Internals.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        /// <summary>
        /// 渲染垂直方向的电路图路径
        /// </summary>
        protected void RenderVerticalCircuitPath(Vector2 p1, Vector2 p4, Vector2 extend1, Vector2 extend2, float midX, float diameter)
        {
            float radius = diameter / 2;

            // 构建路径的关键点
            Vector2 corner1 = new Vector2(p1.x, extend1.y);      // 第一个拐角
            Vector2 corner2 = new Vector2(midX, extend1.y);      // 第二个拐角
            Vector2 corner3 = new Vector2(midX, extend2.y);      // 第三个拐角
            Vector2 corner4 = new Vector2(p4.x, extend2.y);      // 第四个拐角

            // 添加起点
            renderPoints_Internals.Add(p1);

            // 第一个圆角：垂直转水平
            float dir1Y = Mathf.Sign(corner1.y - p1.y);          // 垂直方向
            float dir1X = Mathf.Sign(corner2.x - corner1.x);     // 水平方向
            AddRoundedCorner(corner1, radius, 0, dir1Y, dir1X, 0);

            // 第二个圆角：水平转垂直
            float dir2X = Mathf.Sign(corner2.x - corner1.x);     // 水平方向（从上一段来）
            float dir2Y = Mathf.Sign(corner3.y - corner2.y);     // 垂直方向
            AddRoundedCorner(corner2, radius, dir2X, 0, 0, dir2Y);

            // 第三个圆角：垂直转水平
            float dir3Y = Mathf.Sign(corner3.y - corner2.y);     // 垂直方向（从上一段来）
            float dir3X = Mathf.Sign(corner4.x - corner3.x);     // 水平方向
            AddRoundedCorner(corner3, radius, 0, dir3Y, dir3X, 0);

            // 第四个圆角：水平转垂直
            float dir4X = Mathf.Sign(corner4.x - corner3.x);     // 水平方向（从上一段来）
            float dir4Y = Mathf.Sign(p4.y - corner4.y);          // 垂直方向
            AddRoundedCorner(corner4, radius, dir4X, 0, 0, dir4Y);

            // 添加终点
            renderPoints_Internals.Add(p4);
        }
    }
}