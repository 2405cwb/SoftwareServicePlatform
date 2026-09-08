interface PageHeaderProps {
  title: string;
  buttonText: string;
  onButtonClick: () => void;
}

function PageHeader({
  title,
  buttonText,
  onButtonClick,
}: PageHeaderProps) {
  return (
    <div className="title-row">
      <h2>{title}</h2>

      <button
        className="primary-button"
        onClick={onButtonClick}
      >
        {buttonText}
      </button>
    </div>
  );
}

export default PageHeader;