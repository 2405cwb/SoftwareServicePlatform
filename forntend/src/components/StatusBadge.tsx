/**
 * StatusBadge 组件需要接收的数据
 */
interface StatusBadgeProps {
  enabled: boolean;
}

/**
 * 通用状态标签
 *
 * enabled = true
 * 显示“启用”
 *
 * enabled = false
 * 显示“停用”
 */
function StatusBadge({ enabled }: StatusBadgeProps) {
  return (
    <span className={enabled ? "status-enabled" : "status-disabled"}>
      {enabled ? "启用" : "停用"}
    </span>
  );
}

export default StatusBadge;
