#!/usr/bin/env python3

import argparse
import json
import socket
import sys
import urllib.error
import urllib.request


class HelpFormatter(argparse.ArgumentDefaultsHelpFormatter, argparse.RawDescriptionHelpFormatter):
    pass


def build_parser() -> argparse.ArgumentParser:
    program_name = sys.argv[0] or "client.py"

    return argparse.ArgumentParser(
        description=(
            "Execute a DAX query against the local pbi-rest-proxy REST API. "
            "The DAX query is read from stdin and the successful JSON result is written to stdout."
        ),
        epilog=f"""Sample usage:
  echo 'EVALUATE ROW("Status", "REST")' | python {program_name}
  python {program_name} < query.dax
  python {program_name} --base-url http://127.0.0.1:51087 --timeout 40 < query.dax
""",
        formatter_class=HelpFormatter,
    )


def parse_args() -> argparse.Namespace:
    parser = build_parser()
    parser.add_argument(
        "--base-url",
        default="http://127.0.0.1:51087",
        help="Base URL of the pbi-rest-proxy REST server, without the /execute-dax path. Default: %(default)s",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=40.0,
        help="Total HTTP request timeout in seconds. Default: %(default)s",
    )
    return parser.parse_args()


def read_query_from_stdin() -> str:
    query = sys.stdin.read()
    if not query.strip():
        print("Error: no DAX query was provided on stdin.", file=sys.stderr)
        raise SystemExit(2)

    return query


def build_request(base_url: str, query: str) -> urllib.request.Request:
    payload = json.dumps({"query": query}).encode("utf-8")
    return urllib.request.Request(
        url=f"{base_url.rstrip('/')}/execute-dax",
        data=payload,
        headers={"Content-Type": "application/json; charset=utf-8"},
        method="POST",
    )


def print_http_error(code: int, reason: str, body: bytes) -> None:
    print(f"HTTP {code} {reason}", file=sys.stderr)
    if body:
        print(body.decode("utf-8", errors="replace"), file=sys.stderr)


def main() -> int:
    args = parse_args()
    query = read_query_from_stdin()
    request = build_request(args.base_url, query)

    try:
        with urllib.request.urlopen(request, timeout=args.timeout) as response:
            body = response.read()
            if response.status != 200:
                print_http_error(response.status, response.reason, body)
                return 1

            sys.stdout.buffer.write(body)
            if body and not body.endswith(b"\n"):
                sys.stdout.buffer.write(b"\n")
            return 0
    except urllib.error.HTTPError as exc:
        print_http_error(exc.code, exc.reason, exc.read())
        return 1
    except urllib.error.URLError as exc:
        print(f"HTTP request failed: {exc.reason}", file=sys.stderr)
        return 1
    except TimeoutError:
        print(f"HTTP request timed out after {args.timeout:g} seconds.", file=sys.stderr)
        return 1
    except socket.timeout:
        print(f"HTTP request timed out after {args.timeout:g} seconds.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
