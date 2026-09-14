import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import ReactECharts from "echarts-for-react";
import {
  AlertTriangle,
  Boxes,
  Building2,
  CheckCircle2,
  Clock3,
  Download,
  FileDown,
  Headphones,
  RefreshCw,
  ShieldAlert,
  TicketCheck,
  Tickets,
  UserRoundCheck,
  Users,
  Wrench,
} from "lucide-react";
import { apiFetch } from "../services/api";
import { getSessionUser } from "../utils/session";
import { formatDuration } from "../utils/format";
import KpiCard from "../components/dashboard/KpiCard";
import DashboardTicketList, {
  type DashboardTicket,
} from "../components/dashboard/DashboardTicketList";

interface WorkOverview {
  role: string;
  userId: number;
  displayName: string;
  customerName: string | null;
  myOpenCount: number;
  myUrgentCount: number;
  myResolvedThisMonth: number;
  unassignedCount: number;
  customerSoftwareCount: number;
  salesCustomerCount: number;
  salesSoftwareCount: number;
  recentTickets: DashboardTicket[];
  attentionTickets: DashboardTicket[];
}

interface GlobalSummary {
  activeCustomers: number;
  activeSoftwares: number;
  totalTickets: number;
  openTickets: number;
  thisMonthTickets: number;
  averageFirstResponseMinutes: number | null;
}

interface TicketTrendItem {
  date: string;
  count: number;
}

interface TicketStatusItem {
  status: string;
  name: string;
  count: number;
}

interface EfficiencySummary {
  totalTickets: number;
  openTickets: number;
  respondedTickets: number;
  resolvedTickets: number;
  responseRate: number;
  resolutionRate: number;
  averageFirstResponseMinutes: number | null;
  averageResolutionMinutes: number | null;
}

interface SlaSummary {
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

interface DownloadSummary {
  totalDownloads: number;
  thisMonthDownloads: number;
}

interface RankingItem {
  softwareId?: number | null;
  softwareName?: string;
  customerId?: number | null;
  customerName?: string;
  ticketCount?: number;
}

function DashboardPage() {
  const navigate = useNavigate();
  const currentUser = getSessionUser();
  const role = currentUser?.role ?? "";

  const [overview, setOverview] = useState<WorkOverview | null>(null);
  const [summary, setSummary] = useState<GlobalSummary | null>(null);
  const [trend, setTrend] = useState<TicketTrendItem[]>([]);
  const [status, setStatus] = useState<TicketStatusItem[]>([]);
  const [efficiency, setEfficiency] = useState<EfficiencySummary | null>(null);
  const [sla, setSla] = useState<SlaSummary | null>(null);
  const [downloads, setDownloads] = useState<DownloadSummary | null>(null);
  const [softwareRanking, setSoftwareRanking] = useState<RankingItem[]>([]);
  const [customerRanking, setCustomerRanking] = useState<RankingItem[]>([]);
  const [globalWarningTickets, setGlobalWarningTickets] = useState<DashboardTicket[]>([]);
  const [globalOverdueTickets, setGlobalOverdueTickets] = useState<DashboardTicket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const isManagement = role === "Admin" || role === "Support";

  async function load() {
    try {
      setLoading(true);
      setError("");

      const overviewResponse = await apiFetch("/api/work-dashboard/overview");
      if (!overviewResponse.ok) {
        throw new Error(`加载工作台失败：${overviewResponse.status}`);
      }
      setOverview((await overviewResponse.json()) as WorkOverview);

      if (!isManagement) {
        return;
      }

      const responses = await Promise.all([
        apiFetch("/api/dashboard/summary"),
        apiFetch("/api/dashboard/ticket-trend"),
        apiFetch("/api/dashboard/ticket-status"),
        apiFetch("/api/dashboard/ticket-efficiency-summary"),
        apiFetch("/api/dashboard/ticket-sla-summary"),
        apiFetch("/api/dashboard/download-summary"),
        apiFetch("/api/dashboard/software-ticket-ranking"),
        apiFetch("/api/dashboard/customer-ticket-ranking"),
        apiFetch("/api/dashboard/ticket-sla-warning?take=6"),
        apiFetch("/api/dashboard/ticket-sla-overdue?take=6"),
      ]);

      const failed = responses.find((response) => !response.ok);
      if (failed) {
        throw new Error(`加载管理统计失败：${failed.status}`);
      }

      const [
        summaryData,
        trendData,
        statusData,
        efficiencyData,
        slaData,
        downloadData,
        softwareData,
        customerData,
        warningData,
        overdueData,
      ] = await Promise.all(responses.map((response) => response.json()));

      setSummary(summaryData as GlobalSummary);
      setTrend(trendData as TicketTrendItem[]);
      setStatus(statusData as TicketStatusItem[]);
      setEfficiency(efficiencyData as EfficiencySummary);
      setSla(slaData as SlaSummary);
      setDownloads(downloadData as DownloadSummary);
      setSoftwareRanking(softwareData as RankingItem[]);
      setCustomerRanking(customerData as RankingItem[]);
      setGlobalWarningTickets((warningData as DashboardTicket[]).map((x) => ({ ...x, riskLevel: "warning" })));
      setGlobalOverdueTickets((overdueData as DashboardTicket[]).map((x) => ({ ...x, riskLevel: "danger" })));
    } catch (e) {
      console.error(e);
      setError("数据概览加载失败，请稍后重试。");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, [role]);

  const trendOption = useMemo(
    () => ({
      tooltip: { trigger: "axis" },
      grid: { left: 45, right: 24, top: 28, bottom: 36 },
      xAxis: {
        type: "category",
        boundaryGap: false,
        data: trend.map((x) => x.date.substring(5)),
        axisTick: { show: false },
      },
      yAxis: { type: "value", minInterval: 1 },
      series: [
        {
          name: "新增工单",
          type: "line",
          smooth: true,
          showSymbol: false,
          data: trend.map((x) => x.count),
          lineStyle: { width: 3 },
          areaStyle: { opacity: 0.08 },
        },
      ],
    }),
    [trend],
  );

  const statusOption = useMemo(
    () => ({
      tooltip: { trigger: "item", formatter: "{b}<br/>{c} 个 ({d}%)" },
      legend: { bottom: 0, icon: "circle" },
      series: [
        {
          type: "pie",
          radius: ["54%", "75%"],
          center: ["50%", "44%"],
          itemStyle: { borderRadius: 7, borderWidth: 3, borderColor: "#fff" },
          label: { show: false },
          data: status.map((x) => ({ name: x.name, value: x.count, status: x.status })),
        },
      ],
    }),
    [status],
  );

  function openTicket(ticket: DashboardTicket) {
    navigate(`/tickets?ticketId=${ticket.id}`);
  }

  if (loading) {
    return (
      <div className="content u-page">
        <div className="u-loading-card">正在加载你的工作台...</div>
      </div>
    );
  }

  if (error || !overview) {
    return (
      <div className="content u-page">
        <div className="u-error-card">
          <span>{error || "暂无工作台数据"}</span>
          <button type="button" className="primary-button" onClick={load}>
            重新加载
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div>
          <span className="u-eyebrow">WORKSPACE</span>
          <h2>{getDashboardTitle(role)}</h2>
          <p>{getDashboardSubtitle(role, overview.customerName)}</p>
        </div>
        <button type="button" className="u-secondary-button" onClick={load}>
          <RefreshCw size={16} />
          刷新数据
        </button>
      </header>

      {isManagement && summary ? (
        <ManagementDashboard
          summary={summary}
          overview={overview}
          efficiency={efficiency}
          sla={sla}
          downloads={downloads}
          trendOption={trendOption}
          statusOption={statusOption}
          softwareRanking={softwareRanking}
          customerRanking={customerRanking}
          warningTickets={globalWarningTickets}
          overdueTickets={globalOverdueTickets}
          navigate={navigate}
          openTicket={openTicket}
        />
      ) : role === "Developer" ? (
        <DeveloperDashboard overview={overview} navigate={navigate} openTicket={openTicket} />
      ) : role === "Customer" ? (
        <CustomerDashboard overview={overview} navigate={navigate} openTicket={openTicket} />
      ) : role === "Sales" ? (
        <SalesDashboard overview={overview} navigate={navigate} />
      ) : null}
    </div>
  );
}

function ManagementDashboard({
  summary,
  overview,
  efficiency,
  sla,
  downloads,
  trendOption,
  statusOption,
  softwareRanking,
  customerRanking,
  warningTickets,
  overdueTickets,
  navigate,
  openTicket,
}: {
  summary: GlobalSummary;
  overview: WorkOverview;
  efficiency: EfficiencySummary | null;
  sla: SlaSummary | null;
  downloads: DownloadSummary | null;
  trendOption: object;
  statusOption: object;
  softwareRanking: RankingItem[];
  customerRanking: RankingItem[];
  warningTickets: DashboardTicket[];
  overdueTickets: DashboardTicket[];
  navigate: ReturnType<typeof useNavigate>;
  openTicket: (ticket: DashboardTicket) => void;
}) {
  return (
    <>
      <div className="u-kpi-grid u-kpi-grid-4">
        <KpiCard title="启用客户" value={summary.activeCustomers} description="当前正常服务客户" icon={<Building2 />} onClick={() => navigate("/customers")} />
        <KpiCard title="启用软件" value={summary.activeSoftwares} description="当前启用产品" icon={<Boxes />} tone="purple" onClick={() => navigate("/software")} />
        <KpiCard title="待处理工单" value={summary.openTickets} description="待处理 + 处理中" icon={<Tickets />} tone="orange" onClick={() => navigate("/tickets?status=open")} />
        <KpiCard title="我的待办" value={overview.myOpenCount} description="当前分配给我的工单" icon={<UserRoundCheck />} tone="green" onClick={() => navigate("/tickets?scope=my&status=open")} />
        <KpiCard title="未分配" value={overview.unassignedCount} description="需要售后尽快分诊" icon={<AlertTriangle />} tone="red" onClick={() => navigate("/tickets?scope=unassigned&status=open")} />
        <KpiCard title="本月新增工单" value={summary.thisMonthTickets} description="本月客户问题" icon={<TicketCheck />} tone="slate" onClick={() => navigate("/tickets?period=thisMonth")} />
        <KpiCard title="累计下载" value={downloads?.totalDownloads ?? 0} description="安装包累计下载" icon={<Download />} tone="purple" onClick={() => navigate("/download-records")} />
        <KpiCard title="本月下载" value={downloads?.thisMonthDownloads ?? 0} description="本月安装包下载" icon={<FileDown />} tone="blue" onClick={() => navigate("/download-records?period=thisMonth")} />
      </div>

      <div className="u-grid-2">
        <section className="u-panel">
          <div className="u-panel-head"><div><h3>工单趋势</h3><p>最近30天新增工单</p></div></div>
          <ReactECharts option={trendOption} style={{ height: 310 }} />
        </section>
        <section className="u-panel">
          <div className="u-panel-head"><div><h3>工单状态</h3><p>点击图例了解当前工单分布</p></div></div>
          <ReactECharts
            option={statusOption}
            style={{ height: 310 }}
            onEvents={{
              click: (params: { data?: { status?: string } }) => {
                const clickedStatus = params.data?.status;
                if (clickedStatus) navigate(`/tickets?status=${clickedStatus}`);
              },
            }}
          />
        </section>
      </div>

      <div className="u-section-title"><div><h3>服务效率与 SLA</h3><p>把“处理快不快”和“是否超时”放在同一处观察</p></div></div>
      <div className="u-kpi-grid u-kpi-grid-4">
        <KpiCard title="平均首次响应" value={formatDuration(efficiency?.averageFirstResponseMinutes)} description="有效样本平均值" icon={<Clock3 />} />
        <KpiCard title="平均解决时长" value={formatDuration(efficiency?.averageResolutionMinutes)} description="已解决工单平均值" icon={<Wrench />} tone="green" />
        <KpiCard title="响应 SLA" value={formatRate(sla?.responseComplianceRate)} description={`${sla?.responseMetCount ?? 0}/${sla?.responseEvaluatedCount ?? 0} 达标`} icon={<CheckCircle2 />} tone="green" />
        <KpiCard title="解决 SLA" value={formatRate(sla?.resolutionComplianceRate)} description={`${sla?.resolutionMetCount ?? 0}/${sla?.resolutionEvaluatedCount ?? 0} 达标`} icon={<ShieldAlert />} tone={(sla?.currentResolutionOverdueCount ?? 0) > 0 ? "red" : "blue"} />
      </div>

      <div className="u-grid-2">
        <DashboardTicketList title="即将超时" subtitle="进入 SLA 黄色预警窗口" tickets={warningTickets} onTicketClick={openTicket} emptyText="当前没有即将超时工单" />
        <DashboardTicketList title="已经超时" subtitle="优先处理红色风险工单" tickets={overdueTickets} onTicketClick={openTicket} emptyText="当前没有 SLA 超时工单" />
      </div>

      <div className="u-grid-2">
        <RankingPanel title="软件问题 TOP5" items={softwareRanking.map((x) => ({ id: x.softwareId, name: x.softwareName ?? "-", count: x.ticketCount ?? 0 }))} onClick={(id) => navigate(`/tickets?softwareId=${id}`)} />
        <RankingPanel title="客户问题 TOP5" items={customerRanking.map((x) => ({ id: x.customerId, name: x.customerName ?? "-", count: x.ticketCount ?? 0 }))} onClick={(id) => navigate(`/tickets?customerId=${id}`)} />
      </div>

      <DashboardTicketList title="我的最近工单" subtitle="只显示当前分配给我的工作" tickets={overview.recentTickets} onTicketClick={openTicket} action={<button className="u-link-button" onClick={() => navigate("/tickets?scope=my")}>查看全部</button>} />
    </>
  );
}

function DeveloperDashboard({ overview, navigate, openTicket }: RoleDashboardProps) {
  return (
    <>
      <div className="u-kpi-grid u-kpi-grid-3">
        <KpiCard title="我的待处理" value={overview.myOpenCount} description="待处理 + 处理中" icon={<Wrench />} tone="blue" onClick={() => navigate("/tickets?scope=my&status=open")} />
        <KpiCard title="我的紧急工单" value={overview.myUrgentCount} description="优先查看" icon={<AlertTriangle />} tone="red" onClick={() => navigate("/tickets?scope=my&priority=Urgent&status=open")} />
        <KpiCard title="本月已解决" value={overview.myResolvedThisMonth} description="本月完成数量" icon={<CheckCircle2 />} tone="green" onClick={() => navigate("/tickets?scope=my&status=Resolved&period=thisMonth")} />
      </div>
      <div className="u-grid-2">
        <DashboardTicketList title="需要优先关注" subtitle="紧急、即将超时或已经超时" tickets={overview.attentionTickets} onTicketClick={openTicket} emptyText="当前没有高风险工单" />
        <DashboardTicketList title="最近分配给我的工单" tickets={overview.recentTickets} onTicketClick={openTicket} />
      </div>
    </>
  );
}

function CustomerDashboard({ overview, navigate, openTicket }: RoleDashboardProps) {
  return (
    <>
      <div className="u-customer-welcome">
        <div><span>客户服务门户</span><h3>{overview.customerName || "您的公司"}</h3><p>在这里查看软件、提交问题并跟踪处理进度。</p></div>
        <button className="primary-button" onClick={() => navigate("/tickets?new=1")}>提交新问题</button>
      </div>
      <div className="u-kpi-grid u-kpi-grid-3">
        <KpiCard title="我的软件" value={overview.customerSoftwareCount} description="已授权的软件产品" icon={<Boxes />} tone="purple" onClick={() => navigate("/my-software")} />
        <KpiCard title="进行中的问题" value={overview.myOpenCount} description="公司当前未结束工单" icon={<Headphones />} tone="orange" onClick={() => navigate("/tickets?status=open")} />
        <KpiCard title="本月已解决" value={overview.myResolvedThisMonth} description="本月完成的客户问题" icon={<CheckCircle2 />} tone="green" onClick={() => navigate("/tickets?status=Resolved&period=thisMonth")} />
      </div>
      <DashboardTicketList title="最近服务工单" subtitle="点击可直接进入沟通详情" tickets={overview.recentTickets} onTicketClick={openTicket} hidePriority action={<button className="u-link-button" onClick={() => navigate("/tickets")}>查看全部</button>} />
    </>
  );
}

function SalesDashboard({ overview, navigate }: Omit<RoleDashboardProps, "openTicket">) {
  return (
    <>
      <div className="u-kpi-grid u-kpi-grid-2">
        <KpiCard title="我的客户" value={overview.salesCustomerCount} description="按客户资料中的商务负责人匹配" icon={<Users />} tone="blue" onClick={() => navigate("/customers")} />
        <KpiCard title="客户软件绑定" value={overview.salesSoftwareCount} description="我的客户当前软件绑定数量" icon={<Boxes />} tone="purple" onClick={() => navigate("/customers")} />
      </div>
      <section className="u-panel u-info-panel">
        <h3>销售工作台说明</h3>
        <p>当前数据库的“商务负责人”仍然保存为姓名字符串，因此这里按你的显示名称精确匹配客户。后续如果要做正式的销售客户归属、转交和历史追踪，再单独设计 UserId 关联即可。</p>
      </section>
    </>
  );
}

type RoleDashboardProps = {
  overview: WorkOverview;
  navigate: ReturnType<typeof useNavigate>;
  openTicket: (ticket: DashboardTicket) => void;
};

function RankingPanel({ title, items, onClick }: { title: string; items: { id?: number | null; name: string; count: number }[]; onClick: (id: number) => void }) {
  const max = Math.max(1, ...items.map((x) => x.count));
  return (
    <section className="u-panel">
      <div className="u-panel-head"><div><h3>{title}</h3><p>点击项目直接查看对应工单</p></div></div>
      <div className="u-ranking-list">
        {items.length === 0 ? <div className="u-empty-state">暂无数据</div> : items.map((item, index) => (
          <button key={`${item.id}-${item.name}`} type="button" className="u-ranking-row" disabled={!item.id} onClick={() => item.id && onClick(item.id)}>
            <span className="u-rank-index">{index + 1}</span>
            <span className="u-rank-name">{item.name}</span>
            <span className="u-rank-bar"><i style={{ width: `${(item.count / max) * 100}%` }} /></span>
            <strong>{item.count}</strong>
          </button>
        ))}
      </div>
    </section>
  );
}

function formatRate(value?: number | null) {
  return value === null || value === undefined ? "--" : `${value.toFixed(1)}%`;
}

function getDashboardTitle(role: string) {
  switch (role) {
    case "Admin": return "管理数据概览";
    case "Support": return "售后工作台";
    case "Developer": return "我的开发工作台";
    case "Sales": return "客户运营工作台";
    case "Customer": return "我的服务概览";
    default: return "数据概览";
  }
}

function getDashboardSubtitle(role: string, customerName: string | null) {
  switch (role) {
    case "Admin": return "关注平台运行、工单效率、SLA 风险和客户服务情况";
    case "Support": return "先看未分诊和 SLA 风险，再处理分配给自己的客户问题";
    case "Developer": return "只聚焦分配给你的开发问题和处理风险";
    case "Sales": return "查看自己负责客户的服务与软件绑定概况";
    case "Customer": return customerName ? `${customerName} · 软件与售后服务` : "查看软件与售后服务";
    default: return "实时工作概览";
  }
}

export default DashboardPage;
