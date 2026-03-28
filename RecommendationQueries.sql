-- ============================================================
-- Northwind Recommendation System - PostgreSQL Stored Functions
-- Run after northwind_ddl.sql + northwind_data.sql are loaded
-- psql -U postgres -d northwind -f RecommendationQueries.sql
-- ============================================================

-- 1. COLLABORATIVE FILTERING
CREATE OR REPLACE FUNCTION get_collaborative_recommendations(
    p_customer_id VARCHAR, p_limit INT DEFAULT 10
) RETURNS TABLE (
    product_id INT, product_name VARCHAR, unit_price REAL,
    category_name VARCHAR, supplier_name VARCHAR,
    shared_customers BIGINT, total_purchased BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.unit_price,
           cat.category_name, s.company_name,
           COUNT(DISTINCT o2.customer_id),
           SUM(od2.quantity)::BIGINT
    FROM orders o1
    INNER JOIN order_details od1 ON o1.order_id = od1.order_id
    INNER JOIN order_details od_shared ON od1.product_id = od_shared.product_id
    INNER JOIN orders o2 ON od_shared.order_id = o2.order_id AND o2.customer_id != p_customer_id
    INNER JOIN order_details od2 ON o2.order_id = od2.order_id
    INNER JOIN products p ON od2.product_id = p.product_id
    INNER JOIN categories cat ON p.category_id = cat.category_id
    INNER JOIN suppliers s ON p.supplier_id = s.supplier_id
    WHERE o1.customer_id = p_customer_id AND p.discontinued = FALSE
      AND p.product_id NOT IN (
          SELECT od_e.product_id FROM orders o_e
          INNER JOIN order_details od_e ON o_e.order_id = od_e.order_id
          WHERE o_e.customer_id = p_customer_id)
    GROUP BY p.product_id, p.product_name, p.unit_price, cat.category_name, s.company_name
    ORDER BY 7 DESC, 8 DESC LIMIT p_limit;
END; $$ LANGUAGE plpgsql;

-- 2. TRENDING PRODUCTS
CREATE OR REPLACE FUNCTION get_trending_products(
    p_days INT DEFAULT 90, p_limit INT DEFAULT 10
) RETURNS TABLE (
    product_id INT, product_name VARCHAR, unit_price REAL,
    category_name VARCHAR, supplier_name VARCHAR,
    unique_customers BIGINT, total_orders BIGINT,
    total_quantity BIGINT, total_revenue NUMERIC, daily_velocity NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.unit_price,
           cat.category_name, s.company_name,
           COUNT(DISTINCT o.customer_id),
           COUNT(DISTINCT o.order_id),
           SUM(od.quantity)::BIGINT,
           SUM(od.quantity * od.unit_price * (1 - od.discount))::NUMERIC,
           ROUND(SUM(od.quantity)::NUMERIC / p_days, 2)
    FROM products p
    INNER JOIN categories cat ON p.category_id = cat.category_id
    INNER JOIN suppliers s ON p.supplier_id = s.supplier_id
    INNER JOIN order_details od ON p.product_id = od.product_id
    INNER JOIN orders o ON od.order_id = o.order_id
    WHERE o.order_date >= NOW() - (p_days || ' days')::INTERVAL AND p.discontinued = FALSE
    GROUP BY p.product_id, p.product_name, p.unit_price, cat.category_name, s.company_name
    ORDER BY 9 DESC LIMIT p_limit;
END; $$ LANGUAGE plpgsql;

-- 3. FREQUENTLY BOUGHT TOGETHER
CREATE OR REPLACE FUNCTION get_frequently_bought_together(
    p_product_id INT, p_limit INT DEFAULT 6
) RETURNS TABLE (
    product_id INT, product_name VARCHAR, unit_price REAL,
    category_name VARCHAR, co_occurrence BIGINT, support NUMERIC, lift NUMERIC
) AS $$
DECLARE
    v_total_with INT; v_total_orders INT;
BEGIN
    SELECT COUNT(DISTINCT order_id) INTO v_total_with FROM order_details WHERE product_id = p_product_id;
    SELECT COUNT(DISTINCT order_id) INTO v_total_orders FROM orders;
    RETURN QUERY
    SELECT p.product_id, p.product_name, p.unit_price, cat.category_name,
           COUNT(DISTINCT od2.order_id)::BIGINT,
           ROUND(COUNT(DISTINCT od2.order_id)::NUMERIC / v_total_with, 4),
           ROUND((COUNT(DISTINCT od2.order_id)::NUMERIC / v_total_with) /
                 (COUNT(DISTINCT od2.order_id)::NUMERIC / v_total_orders), 4)
    FROM order_details od1
    INNER JOIN order_details od2 ON od1.order_id = od2.order_id AND od2.product_id != p_product_id
    INNER JOIN products p ON od2.product_id = p.product_id
    INNER JOIN categories cat ON p.category_id = cat.category_id
    WHERE od1.product_id = p_product_id AND p.discontinued = FALSE
    GROUP BY p.product_id, p.product_name, p.unit_price, cat.category_name
    HAVING ROUND(COUNT(DISTINCT od2.order_id)::NUMERIC / v_total_with, 4) >= 0.01
    ORDER BY 7 DESC LIMIT p_limit;
END; $$ LANGUAGE plpgsql;

-- 4. CUSTOMER SEGMENTS VIEW
CREATE OR REPLACE VIEW customer_segments AS
SELECT c.customer_id, c.company_name, c.country,
    COUNT(DISTINCT o.order_id) AS total_orders,
    SUM(od.quantity * od.unit_price * (1 - od.discount))::NUMERIC AS total_spend,
    MAX(o.order_date) AS last_order_date,
    CASE
        WHEN SUM(od.quantity * od.unit_price * (1 - od.discount)) > 10000 AND COUNT(DISTINCT o.order_id) > 10 THEN 'VIP'
        WHEN SUM(od.quantity * od.unit_price * (1 - od.discount)) > 5000  AND COUNT(DISTINCT o.order_id) > 5  THEN 'Loyal'
        WHEN MAX(o.order_date) >= NOW() - INTERVAL '90 days'  THEN 'Active'
        WHEN MAX(o.order_date) >= NOW() - INTERVAL '180 days' THEN 'At Risk'
        ELSE 'Dormant'
    END AS segment
FROM customers c
LEFT JOIN orders o ON c.customer_id = o.customer_id
LEFT JOIN order_details od ON o.order_id = od.order_id
GROUP BY c.customer_id, c.company_name, c.country;

-- Test queries:
-- SELECT * FROM get_collaborative_recommendations('ALFKI', 10);
-- SELECT * FROM get_trending_products(90, 10);
-- SELECT * FROM get_frequently_bought_together(1, 6);
-- SELECT * FROM customer_segments ORDER BY total_spend DESC;
