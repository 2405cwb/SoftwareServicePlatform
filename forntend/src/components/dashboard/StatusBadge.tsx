import { getPriorityName, getStatusName } from "../../utils/format";

export function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`u-badge u-status-${status.toLowerCase()}`}>
      {getStatusName(status)}
    </span>
  );
}

export function PriorityBadge({ priority }: { priority: string }) {
  return (
    <span className={`u-badge u-priority-${priority.toLowerCase()}`}>
      {getPriorityName(priority)}
    </span>
  );
}
