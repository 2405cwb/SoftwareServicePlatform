import { useCallback, useEffect, useState } from "react";

import { Download, FileDown, RefreshCw } from "lucide-react";

import { apiFetch } from "../services/api";
import SearchBar from "../components/SearchBar";

/*
 * 每页显示数量。
 */
const PAGE_SIZE = 20;

/*
 * 后端返回的单条下载记录。
 */
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

/*
 * 后端分页返回结构。
 */
interface DownloadRecordResponse {
  page: number;

  pageSize: number;

  total: number;

  totalPages: number;

  items: DownloadRecordItem[];
}

function DownloadRecordPage() {
  /*
   * 下载记录。
   */
  const [records, setRecords] = useState<DownloadRecordItem[]>([]);

  /*
   * 当前页。
   */
  const [page, setPage] = useState(1);

  /*
   * 总记录数。
   */
  const [total, setTotal] = useState(0);

  /*
   * 总页数。
   */
  const [totalPages, setTotalPages] = useState(0);

  /*
   * 用户正在输入的关键字。
   */
  const [keyword, setKeyword] = useState("");

  /*
   * 真正发送给后端的关键字。
   *
   * 和 keyword 分开是为了做搜索防抖。
   */
  const [searchKeyword, setSearchKeyword] = useState("");

  const [loading, setLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState("");

  /*
   * ==========================================
   * 查询下载记录
   * ==========================================
   */
  const loadRecords = useCallback(async () => {
    try {
      setLoading(true);

      setErrorMessage("");

      const params = new URLSearchParams();

      params.set("page", String(page));

      params.set("pageSize", String(PAGE_SIZE));

      /*
       * 有搜索关键字时才传 keyword。
       */
      if (searchKeyword) {
        params.set("keyword", searchKeyword);
      }

      const response = await apiFetch(
        `/api/download-records?${params.toString()}`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `加载下载记录失败：${response.status}`);
      }

      const data = (await response.json()) as DownloadRecordResponse;

      setRecords(data.items);

      setTotal(data.total);

      setTotalPages(data.totalPages);
    } catch (error) {
      console.error("加载下载记录失败：", error);

      setErrorMessage("加载下载记录失败，请稍后重试。");
    } finally {
      setLoading(false);
    }
  }, [page, searchKeyword]);

  /*
   * 页码、搜索条件变化后重新查询。
   */
  useEffect(() => {
    loadRecords();
  }, [loadRecords]);

  /*
   * ==========================================
   * 搜索防抖
   * ==========================================
   *
   * 用户连续输入时不要每敲一个字
   * 就立刻访问一次后端。
   *
   * 停止输入 350ms 后再搜索。
   */
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);

      setSearchKeyword(keyword.trim());
    }, 350);

    return () => {
      window.clearTimeout(timer);
    };
  }, [keyword]);

  /*
   * 文件大小格式化。
   */
  function formatFileSize(bytes: number) {
    if (bytes <= 0) {
      return "0 B";
    }

    const units = ["B", "KB", "MB", "GB", "TB"];

    let value = bytes;

    let unitIndex = 0;

    while (value >= 1024 && unitIndex < units.length - 1) {
      value /= 1024;

      unitIndex++;
    }

    return `${value.toFixed(unitIndex === 0 ? 0 : 2)} ${units[unitIndex]}`;
  }

  /*
   * UTC 时间转换成本地显示时间。
   */
  function formatDateTime(value: string) {
    return new Date(value).toLocaleString("zh-CN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  }

  return (
    <div className="content download-record-page">
      {/* ==========================================
          页面标题
          ========================================== */}
      <div className="download-record-header">
        <div>
          <div className="download-record-title">
            <Download size={24} />
            下载记录
          </div>

          <div className="download-record-subtitle">
            查看客户软件下载历史及版本使用情况
          </div>
        </div>

        <button
          type="button"
          className="dashboard-refresh-button"
          onClick={loadRecords}
          disabled={loading}
        >
          <RefreshCw
            size={16}
            className={loading ? "dashboard-refresh-spin" : ""}
          />

          {loading ? "正在刷新" : "刷新"}
        </button>
      </div>

      {/* ==========================================
          顶部统计
          ========================================== */}
      <div className="download-record-summary">
        <div className="download-record-summary-icon">
          <FileDown size={22} />
        </div>

        <div>
          <div className="download-record-summary-label">下载记录总数</div>

          <div className="download-record-summary-value">{total}</div>
        </div>
      </div>

      {/* ==========================================
          搜索
          ========================================== */}
      <div className="download-record-toolbar">
        <SearchBar
          value={keyword}
          placeholder="搜索客户、软件、版本、用户名或文件名"
          onChange={setKeyword}
          onClear={() => setKeyword("")}
        />
      </div>

      {/* ==========================================
          错误
          ========================================== */}
      {errorMessage && (
        <div className="download-record-error">{errorMessage}</div>
      )}

      {/* ==========================================
          下载记录表格
          ========================================== */}
      <div className="download-record-card">
        <div className="download-record-table-wrapper">
          <table className="download-record-table">
            <thead>
              <tr>
                <th>客户</th>

                <th>下载用户</th>

                <th>软件</th>

                <th>版本</th>

                <th>文件</th>

                <th>大小</th>

                <th>下载时间</th>
              </tr>
            </thead>

            <tbody>
              {loading && records.length === 0 ? (
                <tr>
                  <td colSpan={7} className="download-record-empty">
                    正在加载下载记录...
                  </td>
                </tr>
              ) : records.length === 0 ? (
                <tr>
                  <td colSpan={7} className="download-record-empty">
                    暂无下载记录
                  </td>
                </tr>
              ) : (
                records.map((record) => (
                  <tr key={record.id}>
                    <td>
                      <div className="download-record-main-text">
                        {record.customerName}
                      </div>
                    </td>

                    <td>
                      <div className="download-record-main-text">
                        {record.userDisplayName || record.userName}
                      </div>

                      {record.userDisplayName && record.userName && (
                        <div className="download-record-secondary-text">
                          {record.userName}
                        </div>
                      )}
                    </td>

                    <td>
                      <div className="download-record-main-text">
                        {record.softwareName}
                      </div>
                    </td>

                    <td>
                      <span className="download-record-version">
                        {record.version}
                      </span>
                    </td>

                    <td>
                      <div
                        className="download-record-file-name"
                        title={record.fileName}
                      >
                        {record.fileName}
                      </div>
                    </td>

                    <td>{formatFileSize(record.fileSize)}</td>

                    <td className="download-record-time">
                      {formatDateTime(record.downloadedAt)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* ======================================
            分页
            ====================================== */}
        <div className="download-record-pagination">
          <div className="download-record-pagination-info">
            共 {total} 条
            {totalPages > 0 && (
              <>
                ，第 {page} / {totalPages} 页
              </>
            )}
          </div>

          <div className="download-record-pagination-buttons">
            <button
              type="button"
              className="normal-button"
              disabled={page <= 1 || loading}
              onClick={() => setPage((value) => value - 1)}
            >
              上一页
            </button>

            <button
              type="button"
              className="normal-button"
              disabled={totalPages === 0 || page >= totalPages || loading}
              onClick={() => setPage((value) => value + 1)}
            >
              下一页
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default DownloadRecordPage;
