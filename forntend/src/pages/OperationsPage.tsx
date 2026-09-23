import { useEffect, useState } from "react";
import {
  Activity,
  AlertTriangle,
  FileClock,
  RefreshCw,
  ShieldAlert,
} from "lucide-react";
import { apiFetch } from "../services/api";

interface DashboardData {
  generatedAt: string;
  summary: {
    errorCount24h: number;
    loginFailureCount24h: number;
    failedUpdateCount24h: number;
    auditCount24h: number;
  };
  recentErrors: Array<{
    id: number;
    level: string;
    source: string;
    message: string;
    detail: string;
    path: string;
    httpMethod: string;
    statusCode: number;
    userName: string;
    ipAddress: string;
    traceId: string;
    createdAt: string;
  }>;
  recentAudits: Array<{
    id: number;
    userName: string;
    displayName: string;
    role: string;
    httpMethod: string;
    path: string;
    statusCode: number;
    durationMs: number;
    ipAddress: string;
    createdAt: string;
  }>;
  recentFailedUpdates: Array<{
    id: number;
    customerName: string;
    softwareName: string;
    fromVersion: string;
    toVersion: string;
    downloadType: string;
    fileCount: number;
    fileSize: number;
    errorMessage: string;
    downloadedAt: string;
  }>;
}

function OperationsPage() {
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");

  async function load() {
    try {
      setLoading(true);
      setErrorMessage("");

      const response = await apiFetch("/api/operations/dashboard?take=50");

      if (!response.ok) {
        throw new Error((await response.text()) || "加载运维中心失败");
      }

      setData((await response.json()) as DashboardData);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "加载运维中心失败",
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  function date(value: string) {
    return new Date(value).toLocaleString("zh-CN");
  }

  const cardStyle = {
    border: "1px solid #e2e8f0",
    borderRadius: 12,
    padding: 16,
    background: "#fff",
  };

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div>
          <span className="u-eyebrow">OPERATIONS</span>
          <h2>运维与审计中心</h2>
          <p>查看最近 API 异常、登录失败、客户端更新失败和后台操作记录。</p>
        </div>

        <button
          type="button"
          className="normal-button"
          onClick={() => void load()}
        >
          <RefreshCw size={16} />
          刷新
        </button>
      </header>

      {loading && <div className="u-loading-card">正在加载运维数据...</div>}

      {!loading && errorMessage && (
        <div className="u-error-card">{errorMessage}</div>
      )}

      {!loading && data && (
        <>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(4,minmax(150px,1fr))",
              gap: 12,
              marginBottom: 18,
            }}
          >
            <div style={cardStyle}>
              <Activity size={18} />
              <div>24h 操作</div>
              <strong style={{ fontSize: 28 }}>
                {data.summary.auditCount24h}
              </strong>
            </div>

            <div style={cardStyle}>
              <AlertTriangle size={18} />
              <div>24h API 错误</div>
              <strong style={{ fontSize: 28 }}>
                {data.summary.errorCount24h}
              </strong>
            </div>

            <div style={cardStyle}>
              <ShieldAlert size={18} />
              <div>24h 登录失败</div>
              <strong style={{ fontSize: 28 }}>
                {data.summary.loginFailureCount24h}
              </strong>
            </div>

            <div style={cardStyle}>
              <FileClock size={18} />
              <div>24h 更新失败</div>
              <strong style={{ fontSize: 28 }}>
                {data.summary.failedUpdateCount24h}
              </strong>
            </div>
          </div>

          <section className="u-panel" style={{ marginBottom: 18 }}>
            <h3>最近客户端更新失败</h3>

            {data.recentFailedUpdates.length === 0 ? (
              <div className="u-empty-state">暂无失败更新。</div>
            ) : (
              <div style={{ display: "grid", gap: 8 }}>
                {data.recentFailedUpdates.map((item) => (
                  <div key={item.id} style={cardStyle}>
                    <strong>
                      {item.customerName || "-"} · {item.softwareName || "-"} ·{" "}
                      {item.fromVersion || "-"} → {item.toVersion || "-"}
                    </strong>

                    <div style={{ marginTop: 6 }}>
                      {item.errorMessage || "未知错误"}
                    </div>

                    <small>
                      {item.downloadType} · {item.fileCount} 个文件 ·{" "}
                      {date(item.downloadedAt)}
                    </small>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="u-panel" style={{ marginBottom: 18 }}>
            <h3>最近系统事件</h3>

            {data.recentErrors.length === 0 ? (
              <div className="u-empty-state">暂无异常事件。</div>
            ) : (
              <div style={{ display: "grid", gap: 8 }}>
                {data.recentErrors.map((item) => (
                  <details key={item.id} style={cardStyle}>
                    <summary style={{ cursor: "pointer" }}>
                      <strong>
                        [{item.level}] {item.source}
                      </strong>{" "}
                      {item.message} · {date(item.createdAt)}
                    </summary>

                    <div style={{ marginTop: 10, fontSize: 13 }}>
                      <div>
                        {item.httpMethod} {item.path} · HTTP {item.statusCode}
                      </div>
                      <div>
                        用户：{item.userName || "-"} · IP：{item.ipAddress || "-"}
                      </div>
                      <div>Trace：{item.traceId}</div>

                      {item.detail && (
                        <pre
                          style={{
                            whiteSpace: "pre-wrap",
                            maxHeight: 280,
                            overflow: "auto",
                          }}
                        >
                          {item.detail}
                        </pre>
                      )}
                    </div>
                  </details>
                ))}
              </div>
            )}
          </section>

          <section className="u-panel">
            <h3>最近操作审计</h3>

            {data.recentAudits.length === 0 ? (
              <div className="u-empty-state">暂无操作记录。</div>
            ) : (
              <div style={{ display: "grid", gap: 8 }}>
                {data.recentAudits.map((item) => (
                  <div key={item.id} style={cardStyle}>
                    <strong>
                      {item.displayName || item.userName || "未知用户"} ·{" "}
                      {item.role}
                    </strong>

                    <div style={{ marginTop: 5 }}>
                      {item.httpMethod} {item.path}
                    </div>

                    <small>
                      HTTP {item.statusCode} · {item.durationMs} ms ·{" "}
                      {date(item.createdAt)} · {item.ipAddress || "-"}
                    </small>
                  </div>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}

export default OperationsPage;
