const BIS_URL = "https://www.bisconsultants.com";

export default function SiteFooter() {
  return (
    <footer className="site-footer">
      Powered By:{" "}
      <a href={BIS_URL} target="_blank" rel="noopener noreferrer">
        BIS Consultants
      </a>
    </footer>
  );
}
