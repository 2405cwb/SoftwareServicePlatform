interface ConfirmDeleteButtonProps {
  message: string;

  onConfirm: () => void;
}

function ConfirmDeleteButton({
  message,
  onConfirm,
}: ConfirmDeleteButtonProps) {
  function handleClick() {
    const confirmed = window.confirm(message);

    if (!confirmed) {
      return;
    }

    onConfirm();
  }

  return (
    <button
      className="delete-button"
      onClick={handleClick}
    >
      删除
    </button>
  );
}

export default ConfirmDeleteButton;