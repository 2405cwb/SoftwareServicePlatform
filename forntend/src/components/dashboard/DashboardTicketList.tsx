import type { ReactNode } from "react";
import { ChevronRight, Inbox } from "lucide-react";
import { PriorityBadge, StatusBadge } from "./StatusBadge";
import { formatShortDateTime } from "../../utils/format";

export interface DashboardTicket {
  id: number;
  ticketNo: string;
  title: string;
  status: string;
  priority: string;
  customerName: string;
  softwareName: string;
  assignedToName?: string | null;
  createdAt: string;
  updatedAt?: string;
  riskLevel?: string;
  responseOverdue?: boolean;
  resolutionOverdue?: boolean;
  responseWarning?: boolean;
  resolutionWarning?: boolean;
}

interface Props {
  title: string;
  subtitle?: string;
  tickets: DashboardTicket[];
  onTicketClick: (ticket: DashboardTicket) => void;
  emptyText?: string;
  action?: ReactNode;
  hidePriority?: boolean;
}

export default function DashboardTicketList({
  title,
  subtitle,
  tickets,
  onTicketClick,
  emptyText = "暂无工单",
  action,
  hidePriority = false,
}: Props) {
  return (
    <section className="u-panel">
      <div className="u-panel-head">
        <div>
          <h3>{title}</h3>
          {subtitle && <p>{subtitle}</p>}
        </div>
        {action}
      </div>

      {tickets.length === 0 ? (
        <div className="u-empty-state">
          <Inbox size={22} />
          <span>{emptyText}</span>
        </div>
      ) : (
        <div className="u-ticket-list">
          {tickets.map((ticket) => (
            <button
              type="button"
              key={ticket.id}
              className={`u-ticket-row ${ticket.riskLevel ? `u-risk-${ticket.riskLevel}` : ""}`}
              onClick={() => onTicketClick(ticket)}
            >
              <div className="u-ticket-main">
                <div className="u-ticket-line">
                  <span className="u-ticket-no">{ticket.ticketNo}</span>
                  {!hidePriority && <PriorityBadge priority={ticket.priority} />}
                  <StatusBadge status={ticket.status} />
                </div>
                <strong>{ticket.title}</strong>
                <div className="u-ticket-meta">
                  <span>{ticket.customerName}</span>
                  <span>·</span>
                  <span>{ticket.softwareName}</span>
                  <span>·</span>
                  <span>{formatShortDateTime(ticket.updatedAt ?? ticket.createdAt)}</span>
                </div>
              </div>
              <ChevronRight size={18} />
            </button>
          ))}
        </div>
      )}
    </section>
  );
}
