import { useNavigate } from "react-router-dom";

function ForbiddenPage() {
  const navigate = useNavigate();

  return (
    <div
      style={{
        padding: "80px 40px",
        textAlign: "center",
      }}
    >
      <div
        style={{
          fontSize: "64px",
          fontWeight: "700",
          color: "#cbd5e1",
        }}
      >
        403
      </div>

      <h2>没有访问权限</h2>

      <p
        style={{
          marginTop: "10px",
          color: "#64748b",
        }}
      >
        当前账号没有权限访问这个功能。
      </p>

      <button
        className="primary-button"
        style={{
          marginTop: "25px",
        }}
        onClick={() =>
          navigate("/", {
            replace: true,
          })
        }
      >
        返回
      </button>
    </div>
  );
}

export default ForbiddenPage;
