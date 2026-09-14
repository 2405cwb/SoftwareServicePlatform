import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Download, FileDown, RefreshCw, Search, X } from "lucide-react";
import { apiFetch } from "../services/api";
import { formatDateTime, formatFileSize } from "../utils/format";

const PAGE_SIZE = 20;

interface DownloadRecordItem {
  id: number;
  userId: number | null;
  userName: string;
  userDisplayName: string;
  customerId: number | null;
  customerName: string;
  softwareId: number | null;
  softwareName: string;
  softwareVersionId: number | null;
  version: string;
  fileName: string;
  fileSize: number;
  downloadedAt: string;
}

interface DownloadRecordResponse {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  items: DownloadRecordItem[];
}

function DownloadRecordPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [records, setRecords] = useState<DownloadRecordItem[]>([]);
  const [page, setPage] = useState(Number(searchParams.get("page") || 1));
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [keyword, setKeyword] = useState(searchParams.get("keyword") ?? "");
  const [searchKeyword, setSearchKeyword] = useState(searchParams.get("keyword") ?? "");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const params = new URLSearchParams(searchParams);
      params.set("page", String(page));
      params.set("pageSize", String(PAGE_SIZE));
      if (searchKeyword) params.set("keyword", searchKeyword);
      else params.delete("keyword");

      const response = await apiFetch(`/api/download-records?${params.toString()}`);
      if (!response.ok) throw new Error(await response.text());

      const data = (await response.json()) as DownloadRecordResponse;
      setRecords(data.items);
      setTotal(data.total);
      setTotalPages(data.totalPages);
    } catch (e) {
      console.error(e);
      setError("加载下载记录失败，请稍后重试。");
    } finally {
      setLoading(false);
    }
  }, [page, searchKeyword, searchParams]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const value = keyword.trim();
      setSearchKeyword(value);
      setPage(1);
      const next = new URLSearchParams(searchParams);
      if (value) next.set("keyword", value);
      else next.delete("keyword");
      next.delete("page");
      setSearchParams(next, { replace: true });
    }, 350);
    return () => window.clearTimeout(timer);
  }, [keyword]);

  function clearFilters() {
    setKeyword("");
    setSearchKeyword("");
    setPage(1);
    setSearchParams({}, { replace: true });
  }

  const hasFilter = !!searchKeyword || searchParams.has("period") || searchParams.has("customerId") || searchParams.has("softwareId");

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div><span className="u-eyebrow">DOWNLOAD AUDIT</span><h2>下载记录</h2><p>追踪客户、软件、版本和安装包的真实下载历史</p></div>
        <button type="button" className="u-secondary-button" onClick={load} disabled={loading}><RefreshCw size={16} />刷新</button>
      </header>

      <div className="u-kpi-grid u-kpi-grid-2 u-download-summary-grid">
        <div className="u-kpi-card"><div className="u-kpi-icon u-kpi-purple"><Download /></div><div className="u-kpi-copy"><div className="u-kpi-title">当前筛选记录</div><div className="u-kpi-value">{total}</div><div className="u-kpi-desc">支持从 Dashboard 带条件跳转</div></div></div>
        <div className="u-kpi-card"><div className="u-kpi-icon u-kpi-blue"><FileDown /></div><div className="u-kpi-copy"><div className="u-kpi-title">当前页</div><div className="u-kpi-value">{page} / {Math.max(totalPages, 1)}</div><div className="u-kpi-desc">每页 {PAGE_SIZE} 条</div></div></div>
      </div>

      <section className="u-panel">
        <div className="u-table-toolbar">
          <div className="u-search-box"><Search size={17} /><input value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="搜索客户、软件、版本、用户名或文件名" /></div>
          {hasFilter && <button type="button" className="u-secondary-button" onClick={clearFilters}><X size={15} />清除筛选</button>}
        </div>

        {error && <div className="u-error-card">{error}</div>}
        {loading ? <div className="u-empty-state">正在加载下载记录...</div> : records.length === 0 ? <div className="u-empty-state">暂无符合条件的下载记录</div> : (
          <div className="u-table-scroll"><table className="u-data-table"><thead><tr><th>下载时间</th><th>客户</th><th>软件 / 版本</th><th>下载用户</th><th>文件</th><th>大小</th></tr></thead><tbody>{records.map((item) => <tr key={item.id}><td className="u-nowrap">{formatDateTime(item.downloadedAt)}</td><td><strong>{item.customerName || "-"}</strong></td><td><strong>{item.softwareName}</strong><small>{item.version}</small></td><td>{item.userDisplayName || item.userName || "-"}</td><td title={item.fileName}>{item.fileName}</td><td className="u-nowrap">{formatFileSize(item.fileSize)}</td></tr>)}</tbody></table></div>
        )}

        {totalPages > 1 && <div className="u-pagination"><button disabled={page <= 1} onClick={() => setPage((x) => Math.max(1, x - 1))}>上一页</button><span>第 {page} / {totalPages} 页</span><button disabled={page >= totalPages} onClick={() => setPage((x) => Math.min(totalPages, x + 1))}>下一页</button></div>}
      </section>
    </div>
  );
}

export default DownloadRecordPage;
