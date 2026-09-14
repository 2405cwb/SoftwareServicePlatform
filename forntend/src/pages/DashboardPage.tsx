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
  Download,
  FileDown,
  ShieldCheck,
  ShieldAlert,
  TriangleAlert,
  TimerOff,
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
interface TicketEfficiencySummary {
  totalTickets: number;

  openTickets: number;

  respondedTickets: number;

  resolvedTickets: number;

  responseRate: number;

  resolutionRate: number;

  averageFirstResponseMinutes: number | null;

  averageResolutionMinutes: number | null;

  averageCloseMinutes: number | null;

  responseSampleCount: number;

  resolutionSampleCount: number;

  closeSampleCount: number;
}
interface TicketSlaSummary {
  slaTicketCount: number;

  responseEvaluatedCount: number;
  responseMetCount: number;
  responseBreachedCount: number;
  currentResponseOverdueCount: number;
  responseComplianceRate: number | null;

  resolutionEvaluatedCount: number;
  resolutionMetCount: number;
  resolutionBreachedCount: number;
  currentResolutionOverdueCount: number;
  resolutionComplianceRate: number | null;
}

interface TicketSlaOverdueItem {
  id: number;

  ticketNo: string;
  title: string;

  priority: string;
  status: string;

  customerName: string;
  softwareName: string;

  assignedToName: string | null;

  createdAt: string;
  firstResponseAt: string | null;

  slaPriority: string | null;

  firstResponseTargetMinutes: number;
  resolutionTargetMinutes: number;

  responseDeadline: string;
  resolutionDeadline: string;

  responseOverdue: boolean;
  resolutionOverdue: boolean;

  responseOverdueMinutes: number;
  resolutionOverdueMinutes: number;

  maxOverdueMinutes: number;
}

interface TicketSlaWarningItem {
  id: number;

  ticketNo: string;
  title: string;

  priority: string;
  status: string;

  customerName: string;
  softwareName: string;

  assignedToName: string | null;

  createdAt: string;
  firstResponseAt: string | null;

  slaPriority: string;

  firstResponseTargetMinutes: number;
  resolutionTargetMinutes: number;

  warningBeforeMinutes: number;

  responseDeadline: string;
  resolutionDeadline: string;

  responseWarning: boolean;
  resolutionWarning: boolean;

  responseRemainingMinutes: number | null;
  resolutionRemainingMinutes: number | null;

  minRemainingMinutes: number;
}

interface StaffTicketEfficiency {
  userId: number;

  username: string;

  displayName: string;

  role: string;

  openTicketCount: number;

  resolvedTicketCount: number;

  averageResolutionMinutes: number | null;

  resolutionSampleCount: number;
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
interface DownloadSummary {
  totalDownloads: number;
  thisMonthDownloads: number;
}

interface DownloadTrendItem {
  date: string;
  count: number;
}

interface SoftwareDownloadRanking {
  softwareId: number | null;
  softwareName: string;
  downloadCount: number;
}

interface CustomerDownloadRanking {
  customerId: number | null;
  customerName: string;
  downloadCount: number;
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
  const [downloadSummary, setDownloadSummary] = useState<DownloadSummary>({
    totalDownloads: 0,
    thisMonthDownloads: 0,
  });

  const [downloadTrend, setDownloadTrend] = useState<DownloadTrendItem[]>([]);

  const [softwareDownloadRanking, setSoftwareDownloadRanking] = useState<
    SoftwareDownloadRanking[]
  >([]);

  const [customerDownloadRanking, setCustomerDownloadRanking] = useState<
    CustomerDownloadRanking[]
  >([]);
  const [loading, setLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState("");
  const [ticketEfficiency, setTicketEfficiency] =
    useState<TicketEfficiencySummary | null>(null);

  const [staffEfficiency, setStaffEfficiency] = useState<
    StaffTicketEfficiency[]
  >([]);

  const [slaSummary, setSlaSummary] = useState<TicketSlaSummary | null>(null);

  const [slaOverdueTickets, setSlaOverdueTickets] = useState<
    TicketSlaOverdueItem[]
  >([]);
  const [slaWarningTickets, setSlaWarningTickets] = useState<
    TicketSlaWarningItem[]
  >([]);
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

        downloadSummaryResponse,
        downloadTrendResponse,
        softwareDownloadRankingResponse,
        customerDownloadRankingResponse,
        ticketEfficiencyResponse,
        staffEfficiencyResponse,
        slaSummaryResponse,
        slaOverdueResponse,
        slaWarningResponse,
      ] = await Promise.all([
        apiFetch("/api/dashboard/summary"),

        apiFetch("/api/dashboard/ticket-status"),

        apiFetch("/api/dashboard/ticket-trend"),

        apiFetch("/api/dashboard/software-ticket-ranking"),

        apiFetch("/api/dashboard/customer-ticket-ranking"),

        apiFetch("/api/dashboard/recent-tickets"),

        apiFetch("/api/dashboard/download-summary"),

        apiFetch("/api/dashboard/download-trend"),

        apiFetch("/api/dashboard/software-download-ranking"),

        apiFetch("/api/dashboard/customer-download-ranking"),
        apiFetch("/api/dashboard/ticket-efficiency-summary"),

        apiFetch("/api/dashboard/staff-ticket-efficiency"),
        apiFetch("/api/dashboard/ticket-sla-summary"),

        apiFetch("/api/dashboard/ticket-sla-overdue?take=10"),
        apiFetch("/api/dashboard/ticket-sla-warning?take=10"),
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
      if (!downloadSummaryResponse.ok) {
        throw new Error(`加载下载汇总失败：${downloadSummaryResponse.status}`);
      }

      if (!downloadTrendResponse.ok) {
        throw new Error(`加载下载趋势失败：${downloadTrendResponse.status}`);
      }

      if (!softwareDownloadRankingResponse.ok) {
        throw new Error(
          `加载软件下载排行失败：${softwareDownloadRankingResponse.status}`,
        );
      }

      if (!customerDownloadRankingResponse.ok) {
        throw new Error(
          `加载客户下载排行失败：${customerDownloadRankingResponse.status}`,
        );
      }
      if (!ticketEfficiencyResponse.ok) {
        throw new Error(
          `加载工单效率统计失败：${ticketEfficiencyResponse.status}`,
        );
      }

      if (!staffEfficiencyResponse.ok) {
        throw new Error(
          `加载人员处理效率失败：${staffEfficiencyResponse.status}`,
        );
      }
      if (!slaSummaryResponse.ok) {
        throw new Error(`加载 SLA 汇总失败：${slaSummaryResponse.status}`);
      }
      if (!slaWarningResponse.ok) {
        throw new Error(`加载 SLA 预警工单失败：${slaWarningResponse.status}`);
      }

      if (!slaOverdueResponse.ok) {
        throw new Error(`加载 SLA 超时工单失败：${slaOverdueResponse.status}`);
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
      const downloadSummaryData =
        (await downloadSummaryResponse.json()) as DownloadSummary;

      const downloadTrendData =
        (await downloadTrendResponse.json()) as DownloadTrendItem[];

      const softwareDownloadRankingData =
        (await softwareDownloadRankingResponse.json()) as SoftwareDownloadRanking[];

      const customerDownloadRankingData =
        (await customerDownloadRankingResponse.json()) as CustomerDownloadRanking[];
      const ticketEfficiencyData =
        (await ticketEfficiencyResponse.json()) as TicketEfficiencySummary;

      const staffEfficiencyData =
        (await staffEfficiencyResponse.json()) as StaffTicketEfficiency[];
      const slaSummaryData =
        (await slaSummaryResponse.json()) as TicketSlaSummary;

      const slaOverdueData =
        (await slaOverdueResponse.json()) as TicketSlaOverdueItem[];

      const slaWarningData =
        (await slaWarningResponse.json()) as TicketSlaWarningItem[];
      setSummary(summaryData);

      setTicketStatus(statusData);

      setTicketTrend(trendData);
      setSoftwareRanking(softwareRankingData);

      setCustomerRanking(customerRankingData);

      setRecentTickets(recentTicketsData);

      setDownloadSummary(downloadSummaryData);

      setDownloadTrend(downloadTrendData);

      setSoftwareDownloadRanking(softwareDownloadRankingData);

      setCustomerDownloadRanking(customerDownloadRankingData);

      setTicketEfficiency(ticketEfficiencyData);

      setStaffEfficiency(staffEfficiencyData);
      setSlaSummary(slaSummaryData);

      setSlaOverdueTickets(slaOverdueData);

      setSlaWarningTickets(slaWarningData);
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

  function formatRate(value: number | null) {
    if (value === null) {
      return "--";
    }

    return `${value.toFixed(1)}%`;
  }

  function formatDuration(minutes: number | null) {
    if (minutes === null) {
      return "--";
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
    [ticketTrend],
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
  const downloadTrendOption = useMemo(
    () => ({
      tooltip: {
        trigger: "axis",

        formatter: (params: any[]) => {
          const item = params?.[0];

          if (!item) {
            return "";
          }

          const source = downloadTrend[item.dataIndex];

          return `
            ${source.date}<br/>
            下载次数：<strong>${source.count}</strong>
          `;
        },
      },

      grid: {
        left: 48,
        right: 25,
        top: 30,
        bottom: 42,
      },

      xAxis: {
        type: "category",

        boundaryGap: false,

        data: downloadTrend.map((item) => item.date.substring(5)),

        axisLine: {
          lineStyle: {
            color: "#dbe3ee",
          },
        },

        axisTick: {
          show: false,
        },

        axisLabel: {
          color: "#64748b",
          interval: 4,
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
          name: "软件下载",

          type: "line",

          smooth: true,

          showSymbol: false,

          symbol: "circle",

          symbolSize: 7,

          data: downloadTrend.map((item) => item.count),

          lineStyle: {
            width: 3,
            color: "#7c3aed",
          },

          itemStyle: {
            color: "#7c3aed",
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
                  color: "rgba(124, 58, 237, 0.25)",
                },
                {
                  offset: 1,
                  color: "rgba(124, 58, 237, 0.02)",
                },
              ],
            },
          },
        },
      ],
    }),
    [downloadTrend],
  );
  const staffEfficiencyOption = useMemo(
    () => ({
      tooltip: {
        trigger: "axis",

        axisPointer: {
          type: "shadow",
        },
      },

      legend: {
        top: 0,

        data: ["已解决", "处理中"],

        textStyle: {
          color: "#64748b",
        },
      },

      grid: {
        left: 25,
        right: 30,
        top: 45,
        bottom: 20,

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

        data: staffEfficiency.map((item) => item.displayName || item.username),

        axisTick: {
          show: false,
        },

        axisLine: {
          show: false,
        },

        axisLabel: {
          color: "#475569",
        },
      },

      series: [
        {
          name: "已解决",

          type: "bar",

          barWidth: 13,

          data: staffEfficiency.map((item) => item.resolvedTicketCount),

          itemStyle: {
            color: "#22c55e",

            borderRadius: [0, 5, 5, 0],
          },
        },

        {
          name: "处理中",

          type: "bar",

          barWidth: 13,

          data: staffEfficiency.map((item) => item.openTicketCount),

          itemStyle: {
            color: "#3b82f6",

            borderRadius: [0, 5, 5, 0],
          },
        },
      ],
    }),

    [staffEfficiency],
  );
  const softwareDownloadRankingOption = useMemo(
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
            下载次数：<strong>${item.value}</strong>
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

        data: softwareDownloadRanking.map((item) => item.softwareName),

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

          data: softwareDownloadRanking.map((item) => item.downloadCount),

          itemStyle: {
            color: "#7c3aed",
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
    [softwareDownloadRanking],
  );

  const customerDownloadRankingOption = useMemo(
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
            下载次数：<strong>${item.value}</strong>
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

        data: customerDownloadRanking.map((item) => item.customerName),

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

          data: customerDownloadRanking.map((item) => item.downloadCount),

          itemStyle: {
            color: "#14b8a6",
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
    [customerDownloadRanking],
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

    {
      title: "累计下载",

      value: downloadSummary.totalDownloads,

      description: "安装包累计下载次数",

      icon: <Download size={22} />,

      className: "dashboard-kpi-indigo",
    },

    {
      title: "本月下载",

      value: downloadSummary.thisMonthDownloads,

      description: "本月安装包下载次数",

      icon: <FileDown size={22} />,

      className: "dashboard-kpi-rose",
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
    工单处理效率
    ================================================= */}
      <div className="dashboard-section-heading">
        <div>
          <div className="dashboard-section-title">工单处理效率</div>

          <div className="dashboard-card-description">
            响应速度、解决效率及人员当前工作量
          </div>
        </div>
      </div>

      {ticketEfficiency && (
        <>
          {/* =============================================
        效率 KPI
        ============================================= */}
          <div className="dashboard-efficiency-grid">
            <div className="dashboard-efficiency-card">
              <div className="dashboard-efficiency-label">平均首次响应</div>

              <div className="dashboard-efficiency-value">
                {formatDuration(ticketEfficiency.averageFirstResponseMinutes)}
              </div>

              <div className="dashboard-efficiency-meta">
                {ticketEfficiency.responseSampleCount} 条有效样本
              </div>
            </div>

            <div className="dashboard-efficiency-card">
              <div className="dashboard-efficiency-label">平均解决时长</div>

              <div className="dashboard-efficiency-value">
                {formatDuration(ticketEfficiency.averageResolutionMinutes)}
              </div>

              <div className="dashboard-efficiency-meta">
                {ticketEfficiency.resolutionSampleCount} 条有效样本
              </div>
            </div>

            <div className="dashboard-efficiency-card">
              <div className="dashboard-efficiency-label">响应率</div>

              <div className="dashboard-efficiency-value">
                {ticketEfficiency.responseRate}%
              </div>

              <div className="dashboard-efficiency-meta">
                {ticketEfficiency.respondedTickets} /{" "}
                {ticketEfficiency.totalTickets} 个工单
              </div>
            </div>

            <div className="dashboard-efficiency-card">
              <div className="dashboard-efficiency-label">解决率</div>

              <div className="dashboard-efficiency-value">
                {ticketEfficiency.resolutionRate}%
              </div>

              <div className="dashboard-efficiency-meta">
                {ticketEfficiency.resolvedTickets} /{" "}
                {ticketEfficiency.totalTickets} 个工单
              </div>
            </div>
          </div>

          {/* =============================================
        人员效率
        ============================================= */}
          <div className="dashboard-efficiency-content">
            {/* 左侧图表 */}
            <div className="dashboard-chart-card">
              <div className="dashboard-card-header">
                <div>
                  <div className="dashboard-card-title">人员处理情况</div>

                  <div className="dashboard-card-description">
                    当前处理中与已解决工单数量
                  </div>
                </div>
              </div>

              {staffEfficiency.length === 0 ? (
                <div className="dashboard-empty">暂无人员处理数据</div>
              ) : (
                <ReactECharts
                  option={staffEfficiencyOption}
                  style={{
                    height: "330px",
                    width: "100%",
                  }}
                />
              )}
            </div>

            {/* 右侧人员表格 */}
            <div className="dashboard-chart-card">
              <div className="dashboard-card-header">
                <div>
                  <div className="dashboard-card-title">处理效率排行</div>

                  <div className="dashboard-card-description">
                    按当前有效已解决工单数量排序
                  </div>
                </div>
              </div>

              <div className="dashboard-staff-table-wrapper">
                <table className="dashboard-staff-table">
                  <thead>
                    <tr>
                      <th>人员</th>

                      <th>角色</th>

                      <th>处理中</th>

                      <th>已解决</th>

                      <th>平均解决</th>
                    </tr>
                  </thead>

                  <tbody>
                    {staffEfficiency.map((staff) => (
                      <tr key={staff.userId}>
                        <td>
                          <div className="dashboard-staff-name">
                            {staff.displayName || staff.username}
                          </div>

                          <div className="dashboard-staff-username">
                            {staff.username}
                          </div>
                        </td>

                        <td>
                          <span
                            className={
                              "dashboard-role-badge " +
                              (staff.role === "Support"
                                ? "dashboard-role-support"
                                : "dashboard-role-developer")
                            }
                          >
                            {staff.role === "Support" ? "售后" : "开发"}
                          </span>
                        </td>

                        <td>{staff.openTicketCount}</td>

                        <td>
                          <strong>{staff.resolvedTicketCount}</strong>
                        </td>

                        <td>
                          {formatDuration(staff.averageResolutionMinutes)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </>
      )}

      {/* =================================================
    SLA 服务质量
    ================================================= */}
      <div className="dashboard-section-heading">
        <div>
          <div className="dashboard-section-title">SLA 服务质量</div>

          <div className="dashboard-card-description">
            根据工单创建时保存的 SLA 规则快照进行统计
          </div>
        </div>
      </div>

      {slaSummary && (
        <>
          {/* =============================================
        SLA KPI
        ============================================= */}
          <div className="dashboard-sla-grid">
            {/* 首次响应达标率 */}
            <div className="dashboard-sla-card">
              <div className="dashboard-sla-icon dashboard-sla-icon-success">
                <ShieldCheck size={22} />
              </div>

              <div>
                <div className="dashboard-sla-label">首次响应 SLA</div>

                <div className="dashboard-sla-value">
                  {formatRate(slaSummary.responseComplianceRate)}
                </div>

                <div className="dashboard-sla-meta">
                  {slaSummary.responseMetCount}
                  {" / "}
                  {slaSummary.responseEvaluatedCount}
                  {" 达标"}
                </div>
              </div>
            </div>

            {/* 解决达标率 */}
            <div className="dashboard-sla-card">
              <div className="dashboard-sla-icon dashboard-sla-icon-success">
                <ShieldCheck size={22} />
              </div>

              <div>
                <div className="dashboard-sla-label">解决 SLA</div>

                <div className="dashboard-sla-value">
                  {formatRate(slaSummary.resolutionComplianceRate)}
                </div>

                <div className="dashboard-sla-meta">
                  {slaSummary.resolutionMetCount}
                  {" / "}
                  {slaSummary.resolutionEvaluatedCount}
                  {" 达标"}
                </div>
              </div>
            </div>

            {/* 当前响应超时 */}
            <div
              className={
                "dashboard-sla-card " +
                (slaSummary.currentResponseOverdueCount > 0
                  ? "dashboard-sla-card-danger"
                  : "")
              }
            >
              <div className="dashboard-sla-icon dashboard-sla-icon-danger">
                <TimerOff size={22} />
              </div>

              <div>
                <div className="dashboard-sla-label">当前响应超时</div>

                <div className="dashboard-sla-value">
                  {slaSummary.currentResponseOverdueCount}
                </div>

                <div className="dashboard-sla-meta">尚未首次响应</div>
              </div>
            </div>

            {/* 当前解决超时 */}
            <div
              className={
                "dashboard-sla-card " +
                (slaSummary.currentResolutionOverdueCount > 0
                  ? "dashboard-sla-card-danger"
                  : "")
              }
            >
              <div className="dashboard-sla-icon dashboard-sla-icon-danger">
                <ShieldAlert size={22} />
              </div>

              <div>
                <div className="dashboard-sla-label">当前解决超时</div>

                <div className="dashboard-sla-value">
                  {slaSummary.currentResolutionOverdueCount}
                </div>

                <div className="dashboard-sla-meta">尚未解决</div>
              </div>
            </div>
          </div>

          {/* =============================================
        没有 SLA 数据时的提示
        ============================================= */}
          {slaSummary.slaTicketCount === 0 && (
            <div className="dashboard-sla-no-data">
              当前还没有使用 SLA 规则创建的新工单。 SLA
              配置不会追溯修改历史工单， 新建工单后这里会开始产生统计数据。
            </div>
          )}
          {/* =============================================
    SLA 即将超时
    ============================================= */}
          <div className="dashboard-sla-warning-card">
            <div className="dashboard-card-header">
              <div>
                <div className="dashboard-card-title">
                  <TriangleAlert size={18} />
                  SLA 即将超时
                </div>

                <div className="dashboard-card-description">
                  已进入 SLA 预警窗口，但尚未真正超时
                </div>
              </div>

              {slaWarningTickets.length > 0 && (
                <div className="dashboard-sla-warning-count">
                  {slaWarningTickets.length} 个预警工单
                </div>
              )}
            </div>

            {slaWarningTickets.length === 0 ? (
              <div className="dashboard-sla-warning-empty">
                当前没有即将超时的工单
              </div>
            ) : (
              <div className="dashboard-sla-table-wrapper">
                <table className="dashboard-sla-table">
                  <thead>
                    <tr>
                      <th>工单</th>

                      <th>优先级</th>

                      <th>客户 / 软件</th>

                      <th>负责人</th>

                      <th>预警类型</th>

                      <th>剩余时间</th>
                    </tr>
                  </thead>

                  <tbody>
                    {slaWarningTickets.map((ticket) => {
                      /*
                       * 同时存在两种预警时，
                       * 展示离截止时间最近的一种。
                       */
                      const remainingMinutes = ticket.resolutionWarning
                        ? ticket.resolutionRemainingMinutes
                        : ticket.responseRemainingMinutes;

                      return (
                        <tr key={ticket.id}>
                          <td>
                            <div className="dashboard-sla-ticket-no">
                              {ticket.ticketNo}
                            </div>

                            <div
                              className="dashboard-sla-ticket-title"
                              title={ticket.title}
                            >
                              {ticket.title}
                            </div>
                          </td>

                          <td>
                            <span
                              className={
                                "dashboard-sla-priority " +
                                `dashboard-sla-priority-${ticket.priority.toLowerCase()}`
                              }
                            >
                              {getPriorityName(ticket.priority)}
                            </span>
                          </td>

                          <td>
                            <div className="dashboard-sla-main-text">
                              {ticket.customerName}
                            </div>

                            <div className="dashboard-sla-secondary-text">
                              {ticket.softwareName}
                            </div>
                          </td>

                          <td>{ticket.assignedToName || "未分配"}</td>

                          <td>
                            <div className="dashboard-sla-warning-tags">
                              {ticket.responseWarning && (
                                <span className="dashboard-sla-warning-tag">
                                  响应预警
                                </span>
                              )}

                              {ticket.resolutionWarning && (
                                <span className="dashboard-sla-warning-tag">
                                  解决预警
                                </span>
                              )}
                            </div>
                          </td>

                          <td>
                            <strong className="dashboard-sla-remaining-time">
                              剩余 {  formatDuration(
      ticket.minRemainingMinutes,
    )}
                            </strong>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
          {/* =============================================
        当前超时工单
        ============================================= */}
          <div className="dashboard-sla-overdue-card">
            <div className="dashboard-card-header">
              <div>
                <div className="dashboard-card-title">
                  <TriangleAlert size={18} />
                  SLA 超时工单
                </div>

                <div className="dashboard-card-description">
                  当前仍处于待处理或处理中的超时工单
                </div>
              </div>

              {slaOverdueTickets.length > 0 && (
                <div className="dashboard-sla-overdue-count">
                  {slaOverdueTickets.length} 个风险工单
                </div>
              )}
            </div>

            {slaOverdueTickets.length === 0 ? (
              <div className="dashboard-sla-safe">
                <ShieldCheck size={24} />

                <div>
                  <div className="dashboard-sla-safe-title">
                    当前没有 SLA 超时工单
                  </div>

                  <div className="dashboard-sla-safe-description">
                    当前需要处理的工单均未超过 SLA 截止时间
                  </div>
                </div>
              </div>
            ) : (
              <div className="dashboard-sla-table-wrapper">
                <table className="dashboard-sla-table">
                  <thead>
                    <tr>
                      <th>工单</th>

                      <th>优先级</th>

                      <th>客户 / 软件</th>

                      <th>负责人</th>

                      <th>超时类型</th>

                      <th>超时时长</th>
                    </tr>
                  </thead>

                  <tbody>
                    {slaOverdueTickets.map((ticket) => {
                      /*
                       * 如果解决已经超时，
                       * 优先展示解决超时。
                       *
                       * 因为解决 SLA
                       * 一般属于更严重的整体超时。
                       */
                      const overdueMinutes = ticket.resolutionOverdue
                        ? ticket.resolutionOverdueMinutes
                        : ticket.responseOverdueMinutes;

                      return (
                        <tr key={ticket.id}>
                          <td>
                            <div className="dashboard-sla-ticket-no">
                              {ticket.ticketNo}
                            </div>

                            <div
                              className="dashboard-sla-ticket-title"
                              title={ticket.title}
                            >
                              {ticket.title}
                            </div>
                          </td>

                          <td>
                            <span
                              className={
                                "dashboard-sla-priority " +
                                `dashboard-sla-priority-${ticket.priority.toLowerCase()}`
                              }
                            >
                              {getPriorityName(ticket.priority)}
                            </span>
                          </td>

                          <td>
                            <div className="dashboard-sla-main-text">
                              {ticket.customerName}
                            </div>

                            <div className="dashboard-sla-secondary-text">
                              {ticket.softwareName}
                            </div>
                          </td>

                          <td>{ticket.assignedToName || "未分配"}</td>

                          <td>
                            <div className="dashboard-sla-breach-tags">
                              {ticket.responseOverdue && (
                                <span className="dashboard-sla-breach-tag">
                                  响应超时
                                </span>
                              )}

                              {ticket.resolutionOverdue && (
                                <span className="dashboard-sla-breach-tag">
                                  解决超时
                                </span>
                              )}
                            </div>
                          </td>

                          <td>
                            <strong className="dashboard-sla-overdue-time">
                              已超时 {formatDuration(overdueMinutes)}
                            </strong>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}

      {/* =================================================
    下载统计
    ================================================= */}
      <div className="dashboard-section-heading">
        <div>
          <div className="dashboard-section-title">下载分析</div>

          <div className="dashboard-card-description">
            软件版本的客户下载与使用情况
          </div>
        </div>
      </div>

      <div className="dashboard-chart-grid">
        <div
          className="
      dashboard-chart-card
      dashboard-full-chart-card
    "
        >
          <div className="dashboard-card-header">
            <div>
              <div className="dashboard-card-title">
                <Download size={18} />
                下载趋势
              </div>

              <div className="dashboard-card-description">
                最近30天软件安装包下载次数
              </div>
            </div>
          </div>

          <ReactECharts
            option={downloadTrendOption}
            style={{
              height: "320px",
              width: "100%",
            }}
          />
        </div>
      </div>

      <div className="dashboard-ranking-grid">
        <div className="dashboard-chart-card">
          <div className="dashboard-card-header">
            <div>
              <div className="dashboard-card-title">软件下载 TOP5</div>

              <div className="dashboard-card-description">
                按安装包下载次数统计
              </div>
            </div>
          </div>

          {softwareDownloadRanking.length === 0 ? (
            <div className="dashboard-empty">暂无软件下载数据</div>
          ) : (
            <ReactECharts
              option={softwareDownloadRankingOption}
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
              <div className="dashboard-card-title">客户下载 TOP5</div>

              <div className="dashboard-card-description">
                按客户安装包下载次数统计
              </div>
            </div>
          </div>

          {customerDownloadRanking.length === 0 ? (
            <div className="dashboard-empty">暂无客户下载数据</div>
          ) : (
            <ReactECharts
              option={customerDownloadRankingOption}
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
