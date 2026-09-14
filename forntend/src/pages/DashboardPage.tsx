import { useEffect, useMemo, useState } from "react";
import ReactECharts from "echarts-for-react";

import {
  Users,
  Package,
  Tickets,
  CircleAlert,
  CalendarDays,
  Clock3,
  RefreshCw,
  TrendingUp,
  ChartPie,
} from "lucide-react";

import { apiFetch } from "../services/api";

/**
 * Dashboard 顶部汇总数据。
 */
interface DashboardSummary {
  activeCustomers: number;
  activeSoftwares: number;
  totalTickets: number;
  openTickets: number;
  thisMonthTickets: number;
  averageFirstResponseMinutes: number | null;
  generatedAtUtc: string;
}

/**
 * 工单状态统计。
 */
interface TicketStatusItem {
  status: string;
  name: string;
  count: number;
}

/**
 * 最近30天工单趋势。
 */
interface TicketTrendItem {
  date: string;
  count: number;
}
interface SoftwareTicketRanking {
  softwareId: number;
  softwareName: string;
  ticketCount: number;
}

interface CustomerTicketRanking {
  customerId: number;
  customerName: string;
  ticketCount: number;
}

interface RecentTicket {
  id: number;
  ticketNo: string;
  title: string;
  status: string;
  priority: string;
  source: string;

  customerName: string;
  softwareName: string;

  assignedToName: string | null;

  createdAt: string;
  updatedAt: string;
}

function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);

  const [ticketStatus, setTicketStatus] = useState<TicketStatusItem[]>([]);

  const [ticketTrend, setTicketTrend] = useState<TicketTrendItem[]>([]);

  const [softwareRanking, setSoftwareRanking] = useState<
    SoftwareTicketRanking[]
  >([]);

  const [customerRanking, setCustomerRanking] = useState<
    CustomerTicketRanking[]
  >([]);

  const [recentTickets, setRecentTickets] = useState<RecentTicket[]>([]);

  const [loading, setLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState("");

  /**
   * ======================================
   * 加载 Dashboard 全部数据
   * ======================================
   */
  async function loadDashboard() {
    try {
      setLoading(true);

      setErrorMessage("");

      /*
       * 三个接口互相没有依赖，
       * 所以并发请求。
       */
      const [
        summaryResponse,
        statusResponse,
        trendResponse,
        softwareRankingResponse,
        customerRankingResponse,
        recentTicketsResponse,
      ] = await Promise.all([
        apiFetch("/api/dashboard/summary"),

        apiFetch("/api/dashboard/ticket-status"),

        apiFetch("/api/dashboard/ticket-trend"),

        apiFetch("/api/dashboard/software-ticket-ranking"),

        apiFetch("/api/dashboard/customer-ticket-ranking"),

        apiFetch("/api/dashboard/recent-tickets"),
      ]);

      if (!summaryResponse.ok) {
        throw new Error(`加载概览失败：${summaryResponse.status}`);
      }

      if (!statusResponse.ok) {
        throw new Error(`加载工单状态失败：${statusResponse.status}`);
      }

      if (!trendResponse.ok) {
        throw new Error(`加载工单趋势失败：${trendResponse.status}`);
      }
      if (!softwareRankingResponse.ok) {
        throw new Error(
          `加载软件问题排行失败：${softwareRankingResponse.status}`,
        );
      }

      if (!customerRankingResponse.ok) {
        throw new Error(
          `加载客户问题排行失败：${customerRankingResponse.status}`,
        );
      }

      if (!recentTicketsResponse.ok) {
        throw new Error(`加载最近工单失败：${recentTicketsResponse.status}`);
      }

      const summaryData = (await summaryResponse.json()) as DashboardSummary;

      const statusData = (await statusResponse.json()) as TicketStatusItem[];

      const trendData = (await trendResponse.json()) as TicketTrendItem[];

      const softwareRankingData =
        (await softwareRankingResponse.json()) as SoftwareTicketRanking[];

      const customerRankingData =
        (await customerRankingResponse.json()) as CustomerTicketRanking[];

      const recentTicketsData =
        (await recentTicketsResponse.json()) as RecentTicket[];

      setSummary(summaryData);

      setTicketStatus(statusData);

      setTicketTrend(trendData);
      setSoftwareRanking(softwareRankingData);

      setCustomerRanking(customerRankingData);

      setRecentTickets(recentTicketsData);
    } catch (error) {
      console.error("加载 Dashboard 失败：", error);

      setErrorMessage("Dashboard 数据加载失败，请稍后重试。");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDashboard();
  }, []);

  /**
   * 平均响应时间显示。
   */
  function formatResponseTime(minutes: number | null) {
    if (minutes === null) {
      return "-";
    }

    if (minutes < 60) {
      return `${minutes.toFixed(1)} 分钟`;
    }

    const hours = minutes / 60;

    if (hours < 24) {
      return `${hours.toFixed(1)} 小时`;
    }

    const days = hours / 24;

    return `${days.toFixed(1)} 天`;
  }

  function getTicketStatusName(status: string) {
    switch (status) {
      case "Pending":
        return "待处理";

      case "Processing":
        return "处理中";

      case "Resolved":
        return "已解决";

      case "Closed":
        return "已关闭";

      default:
        return status;
    }
  }

  function getPriorityName(priority: string) {
    switch (priority) {
      case "Low":
        return "低";

      case "Normal":
        return "普通";

      case "High":
        return "高";

      case "Urgent":
        return "紧急";

      default:
        return priority;
    }
  }

  function formatDateTime(value: string) {
    return new Date(value).toLocaleString("zh-CN", {
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  /**
   * ======================================
   * 折线图配置
   * ======================================
   */
  const trendOption = useMemo(
    () => ({
      tooltip: {
        trigger: "axis",

        formatter: (params: any[]) => {
          const item = params?.[0];

          if (!item) {
            return "";
          }

          const index = item.dataIndex;

          const source = ticketTrend[index];

          return `
      ${source.date}<br/>
      新增工单：<strong>${source.count}</strong>
    `;
        },
      },

      grid: {
        left: 48,
        right: 20,
        top: 30,
        bottom: 42,
      },

      xAxis: {
        type: "category",

        boundaryGap: false,

        data: ticketTrend.map((item) => item.date.substring(5)),

        axisLine: {
          lineStyle: {
            color: "#dbe3ee",
          },
        },

        axisLabel: {
          color: "#64748b",

          interval: 4,
        },

        axisTick: {
          show: false,
        },
      },

      yAxis: {
        type: "value",

        minInterval: 1,

        axisLabel: {
          color: "#64748b",
        },

        splitLine: {
          lineStyle: {
            color: "#edf1f5",
          },
        },
      },

      series: [
        {
          name: "新增工单",

          type: "line",

          smooth: true,

          symbol: "circle",

          symbolSize: 7,

          showSymbol: false,

          data: ticketTrend.map((item) => item.count),

          lineStyle: {
            width: 3,
            color: "#2563eb",
          },

          itemStyle: {
            color: "#2563eb",
          },

          areaStyle: {
            color: {
              type: "linear",

              x: 0,
              y: 0,
              x2: 0,
              y2: 1,

              colorStops: [
                {
                  offset: 0,
                  color: "rgba(37, 99, 235, 0.28)",
                },
                {
                  offset: 1,
                  color: "rgba(37, 99, 235, 0.02)",
                },
              ],
            },
          },

          emphasis: {
            focus: "series",
          },
        },
      ],
    }),
    [ticketStatus],
  );
  const totalTickets = summary?.totalTickets ?? 0;
  /**
   * ======================================
   * 工单状态环形图
   * ======================================
   */
  const statusOption = useMemo(
    () => ({
      tooltip: {
        trigger: "item",

        formatter: "{b}<br/>{c} 个工单 ({d}%)",
      },

      legend: {
        bottom: 0,

        icon: "circle",

        textStyle: {
          color: "#64748b",
        },
      },
      graphic: [
        {
          type: "text",

          left: "center",

          top: "34%",

          style: {
            text: String(totalTickets),

            textAlign: "center",

            fill: "#0f172a",

            fontSize: 26,

            fontWeight: 700,
          },
        },

        {
          type: "text",

          left: "center",

          top: "45%",

          style: {
            text: "全部工单",

            textAlign: "center",

            fill: "#94a3b8",

            fontSize: 12,
          },
        },
      ],
      series: [
        {
          name: "工单状态",

          type: "pie",

          radius: ["55%", "75%"],

          center: ["50%", "43%"],

          avoidLabelOverlap: true,

          itemStyle: {
            borderRadius: 6,

            borderColor: "#ffffff",

            borderWidth: 3,
          },

          label: {
            show: false,
          },

          emphasis: {
            label: {
              show: true,

              fontSize: 18,

              fontWeight: "bold",
            },
          },

          data: ticketStatus.map((item) => ({
            value: item.count,

            name: item.name,
          })),
        },
      ],
    }),
    [ticketStatus, totalTickets],
  );
  const softwareRankingOption = useMemo(
    () => ({
      tooltip: {
        trigger: "axis",
        axisPointer: {
          type: "shadow",
        },
        formatter: (params: any[]) => {
          const item = params?.[0];

          if (!item) {
            return "";
          }

          return `
            ${item.name}<br/>
            工单数量：<strong>${item.value}</strong>
          `;
        },
      },

      grid: {
        left: 20,
        right: 35,
        top: 20,
        bottom: 15,
        containLabel: true,
      },

      xAxis: {
        type: "value",
        minInterval: 1,

        axisLabel: {
          color: "#94a3b8",
        },

        splitLine: {
          lineStyle: {
            color: "#f1f5f9",
          },
        },
      },

      yAxis: {
        type: "category",

        inverse: true,

        data: softwareRanking.map((item) => item.softwareName),

        axisTick: {
          show: false,
        },

        axisLine: {
          show: false,
        },

        axisLabel: {
          width: 150,
          overflow: "truncate",
          color: "#475569",
        },
      },

      series: [
        {
          type: "bar",

          barWidth: 18,

          data: softwareRanking.map((item) => item.ticketCount),

          itemStyle: {
            color: "#6366f1",
            borderRadius: [0, 6, 6, 0],
          },

          label: {
            show: true,
            position: "right",
            color: "#475569",
          },
        },
      ],
    }),
    [softwareRanking],
  );

  const customerRankingOption = useMemo(
    () => ({
      tooltip: {
        trigger: "axis",

        axisPointer: {
          type: "shadow",
        },

        formatter: (params: any[]) => {
          const item = params?.[0];

          if (!item) {
            return "";
          }

          return `
            ${item.name}<br/>
            工单数量：<strong>${item.value}</strong>
          `;
        },
      },

      grid: {
        left: 20,
        right: 35,
        top: 20,
        bottom: 15,
        containLabel: true,
      },

      xAxis: {
        type: "value",

        minInterval: 1,

        axisLabel: {
          color: "#94a3b8",
        },

        splitLine: {
          lineStyle: {
            color: "#f1f5f9",
          },
        },
      },

      yAxis: {
        type: "category",

        inverse: true,

        data: customerRanking.map((item) => item.customerName),

        axisTick: {
          show: false,
        },

        axisLine: {
          show: false,
        },

        axisLabel: {
          width: 150,
          overflow: "truncate",
          color: "#475569",
        },
      },

      series: [
        {
          type: "bar",

          barWidth: 18,

          data: customerRanking.map((item) => item.ticketCount),

          itemStyle: {
            color: "#0ea5e9",
            borderRadius: [0, 6, 6, 0],
          },

          label: {
            show: true,
            position: "right",
            color: "#475569",
          },
        },
      ],
    }),
    [customerRanking],
  );

  if (loading) {
    return (
      <div className="content">
        <div className="dashboard-loading">正在加载数据概览...</div>
      </div>
    );
  }

  if (errorMessage || summary === null) {
    return (
      <div className="content">
        <div className="dashboard-error">
          <div>{errorMessage || "暂无 Dashboard 数据"}</div>

          <button className="primary-button" onClick={loadDashboard}>
            重新加载
          </button>
        </div>
      </div>
    );
  }

  const cards = [
    {
      title: "启用客户",

      value: summary.activeCustomers,

      description: "当前正常服务客户",

      icon: <Users size={22} />,

      className: "dashboard-kpi-blue",
    },

    {
      title: "启用软件",

      value: summary.activeSoftwares,

      description: "当前启用软件产品",

      icon: <Package size={22} />,

      className: "dashboard-kpi-purple",
    },

    {
      title: "历史工单",

      value: summary.totalTickets,

      description: "平台累计工单数量",

      icon: <Tickets size={22} />,

      className: "dashboard-kpi-slate",
    },

    {
      title: "待处理工单",

      value: summary.openTickets,

      description: "待处理 + 处理中",

      icon: <CircleAlert size={22} />,

      className: "dashboard-kpi-orange",
    },

    {
      title: "本月新增",

      value: summary.thisMonthTickets,

      description: "本月新增客户工单",

      icon: <CalendarDays size={22} />,

      className: "dashboard-kpi-green",
    },

    {
      title: "平均首次响应",

      value: formatResponseTime(summary.averageFirstResponseMinutes),

      description: "客服首次公开响应",

      icon: <Clock3 size={22} />,

      className: "dashboard-kpi-cyan",
    },
  ];

  return (
    <div className="content dashboard-page">
      {/* ==============================
          页面头部
          ============================== */}
      <div className="dashboard-header">
        <div>
          <div className="dashboard-title">数据概览</div>

          <div className="dashboard-subtitle">
            实时了解客户、软件及工单运行情况
          </div>
        </div>

        <button
          type="button"
          className="dashboard-refresh-button"
          onClick={loadDashboard}
          disabled={loading}
        >
          <RefreshCw
            size={16}
            className={loading ? "dashboard-refresh-spin" : ""}
          />

          {loading ? "正在刷新" : "刷新数据"}
        </button>
      </div>

      {/* ==============================
          KPI
          ============================== */}
      <div className="dashboard-kpi-grid">
        {cards.map((card) => (
          <div className="dashboard-kpi-card" key={card.title}>
            <div className={"dashboard-kpi-icon " + card.className}>
              {card.icon}
            </div>

            <div className="dashboard-kpi-title">{card.title}</div>

            <div className="dashboard-kpi-value">{card.value}</div>

            <div className="dashboard-kpi-description">{card.description}</div>
          </div>
        ))}
      </div>

      {/* ==============================
          图表
          ============================== */}
      <div className="dashboard-chart-grid">
        {/* 工单趋势 */}
        <div className="dashboard-chart-card dashboard-trend-card">
          <div className="dashboard-card-header">
            <div>
              <div className="dashboard-card-title">
                <TrendingUp size={18} />
                工单趋势
              </div>

              <div className="dashboard-card-description">
                最近30天客户工单新增情况
              </div>
            </div>
          </div>

          <ReactECharts
            option={trendOption}
            style={{
              height: "340px",
              width: "100%",
            }}
          />
        </div>

        {/* 工单状态 */}
        <div className="dashboard-chart-card">
          <div className="dashboard-card-header">
            <div>
              <div className="dashboard-card-title">
                <ChartPie size={18} />
                工单状态
              </div>

              <div className="dashboard-card-description">
                当前工单状态整体分布
              </div>
            </div>
          </div>

          <ReactECharts
            option={statusOption}
            style={{
              height: "340px",
              width: "100%",
            }}
          />
        </div>
      </div>
      {/* =================================================
    TOP5 排行
    ================================================= */}
      <div className="dashboard-ranking-grid">
        <div className="dashboard-chart-card">
          <div className="dashboard-card-header">
            <div>
              <div className="dashboard-card-title">软件问题 TOP5</div>

              <div className="dashboard-card-description">
                按历史工单数量统计问题较多的软件
              </div>
            </div>
          </div>

          {softwareRanking.length === 0 ? (
            <div className="dashboard-empty">暂无软件工单数据</div>
          ) : (
            <ReactECharts
              option={softwareRankingOption}
              style={{
                height: "300px",
                width: "100%",
              }}
            />
          )}
        </div>

        <div className="dashboard-chart-card">
          <div className="dashboard-card-header">
            <div>
              <div className="dashboard-card-title">客户问题 TOP5</div>

              <div className="dashboard-card-description">
                按历史工单数量统计售后需求较多的客户
              </div>
            </div>
          </div>

          {customerRanking.length === 0 ? (
            <div className="dashboard-empty">暂无客户工单数据</div>
          ) : (
            <ReactECharts
              option={customerRankingOption}
              style={{
                height: "300px",
                width: "100%",
              }}
            />
          )}
        </div>
      </div>

      {/* =================================================
    最近工单
    ================================================= */}
      <div className="dashboard-recent-card">
        <div className="dashboard-card-header">
          <div>
            <div className="dashboard-card-title">最近工单</div>

            <div className="dashboard-card-description">
              最近提交的客户问题与当前处理状态
            </div>
          </div>
        </div>

        {recentTickets.length === 0 ? (
          <div className="dashboard-empty">暂无工单数据</div>
        ) : (
          <div className="dashboard-table-wrapper">
            <table className="dashboard-table">
              <thead>
                <tr>
                  <th>工单编号</th>

                  <th>问题标题</th>

                  <th>客户</th>

                  <th>软件</th>

                  <th>状态</th>

                  <th>优先级</th>

                  <th>处理人</th>

                  <th>创建时间</th>
                </tr>
              </thead>

              <tbody>
                {recentTickets.map((ticket) => (
                  <tr key={ticket.id}>
                    <td>
                      <span className="dashboard-ticket-no">
                        {ticket.ticketNo}
                      </span>
                    </td>

                    <td>
                      <div
                        className="dashboard-ticket-title"
                        title={ticket.title}
                      >
                        {ticket.title}
                      </div>
                    </td>

                    <td>{ticket.customerName}</td>

                    <td>{ticket.softwareName}</td>

                    <td>
                      <span
                        className={
                          "dashboard-status-badge " +
                          `dashboard-status-${ticket.status.toLowerCase()}`
                        }
                      >
                        {getTicketStatusName(ticket.status)}
                      </span>
                    </td>

                    <td>
                      <span
                        className={
                          "dashboard-priority-badge " +
                          `dashboard-priority-${ticket.priority.toLowerCase()}`
                        }
                      >
                        {getPriorityName(ticket.priority)}
                      </span>
                    </td>

                    <td>{ticket.assignedToName || "未分配"}</td>

                    <td>{formatDateTime(ticket.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

export default DashboardPage;
