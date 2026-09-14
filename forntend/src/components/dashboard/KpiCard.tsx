import type { ReactNode } from "react";
import { ChevronRight } from "lucide-react";

interface Props {
  title: string;
  value: ReactNode;
  description?: string;
  icon: ReactNode;
  tone?: "blue" | "green" | "orange" | "purple" | "red" | "slate";
  onClick?: () => void;
}

export default function KpiCard({
  title,
  value,
  description,
  icon,
  tone = "blue",
  onClick,
}: Props) {
  const content = (
    <>
      <div className={`u-kpi-icon u-kpi-${tone}`}>{icon}</div>
      <div className="u-kpi-copy">
        <div className="u-kpi-title">{title}</div>
        <div className="u-kpi-value">{value}</div>
        {description && <div className="u-kpi-desc">{description}</div>}
      </div>
      {onClick && <ChevronRight className="u-kpi-arrow" size={18} />}
    </>
  );

  if (!onClick) {
    return <div className="u-kpi-card">{content}</div>;
  }

  return (
    <button type="button" className="u-kpi-card u-kpi-clickable" onClick={onClick}>
      {content}
    </button>
  );
}
