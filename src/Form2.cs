using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace CyControl
{
    public partial class Form2 : Form
    {
        // vendor variables
        public double[] dataFrame = new double[90000];
        public int currentPrecision = (int)Precision.x1;
        private Form1 f1 = null;
        private int varRange;

        public void updateForm2()
        {
            int boundary = (this.currentPrecision * 1024 / 4);
            labelPrecision.Text = "Precision: " + this.currentPrecision.ToString();

            // update chart scale
            switch ((Precision)currentPrecision)
            {
                case Precision.x1:
                    varRange = 44;
                    break;
                case Precision.x2:
                    varRange = 18;
                    break;
                case Precision.x4:
                    varRange = 10;
                    break;
                case Precision.x8:
                    varRange = 5;
                    break;
                case Precision.x16:
                case Precision.x32:
                    varRange = 4;
                    break;
                default:
                    varRange = 44;
                    break;
            }


            // clear and update series
            this.chart1.DataBind();
            
            this.chart1.ChartAreas[0].AxisY.LabelStyle.Format = "0.000000";
            this.chart1.ChartAreas[0].AxisX.LabelStyle.Format = "0";
            this.chart1.ChartAreas[0].AxisY.Maximum = 5;
            this.chart1.ChartAreas[0].AxisY.Minimum = -5;
            this.chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
            this.chart1.ChartAreas[0].AxisY.MajorTickMark.Enabled = false;
            //this.chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            this.chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;
            this.chart1.MouseWheel += chart1_MouseWheel;
            foreach (var s in this.chart1.Series)
            {   
                
                s.Points.Clear();
                s.Name = "650 Data, Precision: " + this.currentPrecision.ToString();
                s.Color = Color.Blue;
                s.ChartType = SeriesChartType.Line;
                
                

                for (int index = 0; index < boundary; index++)
                {
                    s.Points.AddXY(index, dataFrame[index]);
                }
                break;
                
            }
        }


        public Form2(Form1 f1)
        {
            InitializeComponent();
            initCharts();
            this.f1 = f1;
            this.varRange = 44;
        }

        // vendor functions 
        private void initCharts()
        {

            int[,] array = new int[,] {
            {1,8,9,7,105,11,50,999,500,1},
            {12,15,11,18,733,5,4,3,2,500} };


            //標題 最大數值
            Series series1 = new Series("650 Data", 1);
            

            //設定線條顏色
            series1.Color = Color.Blue;
            

            //設定字型
            series1.Font = new System.Drawing.Font("新細明體", 14);
            

            //折線圖
            series1.ChartType = SeriesChartType.Line;
           

            //將數值顯示在線上
            //series1.IsValueShownAsLabel = true;
            


            //將數值新增至序列
            //for (int index = 0; index < 10; index++)
            //{
            //    series1.Points.AddXY(index, array[0, index]);
                
            //}

            //將序列新增到圖上
            this.chart1.Series.Add(series1);
            

            //標題
            this.chart1.Titles.Add("650 Data Analysis");
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            
        }


        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.f1.funcRenewForm2();
        }

        private void funcTestChart()
        {
            Random r = new Random();
            for (int cnt = 0; cnt < dataFrame.Length; cnt+= 1000)
            {
                this.dataFrame[cnt] = r.Next(-1, 2);
                
            }
            this.updateForm2();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            funcTestChart();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            // clear and update series
            foreach (var s in this.chart1.Series)
            {
                s.Points.Clear();
            }
        }

        private void chart1_MouseWheel(object sender, MouseEventArgs e)
        {
            var chart = (Chart)sender;
            var xAxis = chart.ChartAreas[0].AxisX;
            var yAxis = chart.ChartAreas[0].AxisY;

            try
            {
                if (e.Delta < 0) // Scrolled down.
                {
                    xAxis.ScaleView.ZoomReset();
                    yAxis.ScaleView.ZoomReset();
                }
                else if (e.Delta > 0) // Scrolled up.
                {
                    var xMin = xAxis.ScaleView.ViewMinimum;
                    var xMax = xAxis.ScaleView.ViewMaximum;
                    var yMin = yAxis.ScaleView.ViewMinimum;
                    var yMax = yAxis.ScaleView.ViewMaximum;

                    var posXStart = xAxis.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 1.5;
                    var posXFinish = xAxis.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 1.5;
                    var posYStart = yAxis.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 3;
                    var posYFinish = yAxis.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 3;

                    //xAxis.ScaleView.Zoom(posXStart, posXFinish);
                    yAxis.ScaleView.Zoom(posYStart, posYFinish);
                }
            }
            catch { }
        }
    }
    public enum Precision
    {
        x1 = 344,
        x2 = 141,
        x4 = 83,
        x8 = 40,
        x16= 34,
        x32= 32
    }

    public struct Coords
    {
        public Coords(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public override string ToString() => $"({X}, {Y})";
    }



}
